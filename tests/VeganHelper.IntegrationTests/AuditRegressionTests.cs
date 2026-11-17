using System.Net;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkiaSharp;
using VeganHelper.DAL.Storage;
using VeganHelper.DAL.Entities;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class AuditRegressionTests(Member2ApiFixture fixture)
{
    private const string Password = "ChefPassword!123";

    [Fact]
    public async Task LoginUsesEmailOwnerEvenWhenAnotherUserHasThatUsername()
    {
        var owner = await SeedUserAsync();
        await SeedUserAsync(username: owner.Email);
        using var client = fixture.Factory.Client();
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { identifier = owner.Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var tokens = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        Assert.Equal(owner.Id.ToString(CultureInfo.InvariantCulture), tokens.ReadJwtToken(result.GetProperty("accessToken").GetString()).Subject);
    }

    [Fact]
    public async Task FailedSignInsAreLimitedWithoutDisablingExistingAdminSession()
    {
        var admin = await SeedUserAsync(role: 2);
        using var guest = fixture.Factory.Client();
        using var session = fixture.Factory.Client(admin.Id);
        for (var i = 0; i < 5; i++)
        {
            using var response = await guest.PostAsJsonAsync("/api/auth/login", new { identifier = admin.Email, password = "wrong password" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        using var blocked = await guest.PostAsJsonAsync("/api/auth/login", new { identifier = admin.Email, password = Password });
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.True(blocked.Headers.Contains("Retry-After"));
        using var report = await session.GetAsync("/api/admin/audit-logs");
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
    }

    [Fact]
    public async Task ProfileMeasurementsShareBmiHistoryAndRejectInvalidAvatar()
    {
        var user = await SeedUserAsync();
        using var client = fixture.Factory.Client(user.Id);
        using var initial = await client.PutAsJsonAsync("/api/HealthProfile", new
        {
            heightCm = 175, weightKg = 70, birthDate = "1996-01-01", biologicalSex = "male",
            dietType = "vegan", activityLevel = "sedentary"
        });
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        for (var i = 0; i < 2; i++)
        {
            using var profile = new MultipartFormDataContent { { new StringContent("74"), "weightKg" } };
            using var update = await client.PutAsync("/api/users/me", profile);
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        }
        using var payload = new MultipartFormDataContent();
        var fake = new ByteArrayContent(Encoding.UTF8.GetBytes("this is not an image"));
        fake.Headers.ContentType = new("image/png");
        payload.Add(fake, "avatar", "fake.png");
        using var rejected = await client.PutAsync("/api/users/me", payload);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        await using var db = fixture.Context();
        var stored = await db.UserProfiles.SingleAsync(p => p.UserId == user.Id);
        Assert.Equal(24.16m, stored.CurrentBmi);
        Assert.Equal(2, await db.BmiHistories.CountAsync(h => h.UserId == user.Id));
    }

    [Fact]
    public async Task CustomAllergyIsPrivateAndRecognizesPeanutButterOnPosts()
    {
        var user = await SeedUserAsync();
        var other = await SeedUserAsync();
        using var owner = fixture.Factory.Client(user.Id);
        using var stranger = fixture.Factory.Client(other.Id);
        var customName = "test private " + Guid.NewGuid().ToString("N")[..12];
        using var allergy = await owner.PutAsJsonAsync("/api/HealthProfile/allergies", new
        {
            allergyIngredientIds = Array.Empty<long>(),
            customAllergies = new[] { "đậu phộng", customName }
        });
        Assert.True(allergy.IsSuccessStatusCode);
        await using var db = fixture.Context();
        Assert.False(await db.Ingredients.AnyAsync(i => i.Name == customName));
        Assert.False(await db.Set<CustomUserAllergy>().AnyAsync(x => x.UserId == other.Id));
        var peanutButter = new Ingredient { Name = "Peanut butter E2E " + Guid.NewGuid().ToString("N")[..8], DefaultUnit = "g" };
        var post = new Post { AuthorId = other.Id, Title = "Toast recipe", Status = "published", CreatedAt = DateTime.UtcNow };
        db.AddRange(peanutButter, post);
        await db.SaveChangesAsync();
        db.PostIngredients.Add(new PostIngredient { PostId = post.Id, IngredientId = peanutButter.Id, Unit = "g" });
        await db.SaveChangesAsync();
        var warnings = await owner.GetFromJsonAsync<JsonElement>($"/api/posts/{post.Id}/allergy-warnings");
        var otherWarnings = await stranger.GetFromJsonAsync<JsonElement>($"/api/posts/{post.Id}/allergy-warnings");
        Assert.Single(warnings.GetProperty("allergens").EnumerateArray());
        Assert.Empty(otherWarnings.GetProperty("allergens").EnumerateArray());
    }

    [Fact]
    public async Task SetPasswordIsSingleUseAndLogoutRevokesTheIssuedRefreshToken()
    {
        var user = await SeedUserAsync();
        await using (var db = fixture.Context())
            await db.Users.Where(x => x.Id == user.Id).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.PasswordHash, (string?)null));
        using var session = fixture.Factory.Client(user.Id);
        var setRequest = new { newPassword = Password, confirmPassword = Password };
        using var set = await session.PostAsJsonAsync("/api/auth/set-password", setRequest);
        using var repeated = await session.PostAsJsonAsync("/api/auth/set-password", setRequest);
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        using var guest = fixture.Factory.Client();
        using var login = await guest.PostAsJsonAsync("/api/auth/login", new { identifier = user.Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var refreshToken = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refreshToken").GetString();
        using var logout = await session.PostAsJsonAsync("/api/auth/logout", new { refreshToken });
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        using var refresh = await guest.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task ConfirmingNewEmailInvalidatesTheOldSession()
    {
        var user = await SeedUserAsync();
        var newEmail = Guid.NewGuid().ToString("N") + "@example.invalid";
        const string otp = "759281";
        await using (var db = fixture.Context())
        {
            await db.Users.Where(x => x.Id == user.Id).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.PendingEmail, newEmail));
            db.EmailVerificationTokens.Add(new EmailVerificationToken
            {
                UserId = user.Id, TargetEmail = newEmail, Purpose = "email_change_new", CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                TokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(otp))).ToLowerInvariant()
            });
            await db.SaveChangesAsync();
        }
        using var session = fixture.Factory.Client(user.Id);
        using var guest = fixture.Factory.Client();
        using var login = await guest.PostAsJsonAsync("/api/auth/login", new { identifier = user.Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var refreshToken = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refreshToken").GetString();
        using var confirmed = await session.PostAsJsonAsync("/api/auth/change-email/confirm", new { otp });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        using var profile = await session.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);
        using var refresh = await guest.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task ConcurrentAvatarUpdatesQueueOnlyTheReplacedManagedObjects()
    {
        var user = await SeedUserAsync();
        var avatar = new RecordingAvatarStorage();
        await using (var db = fixture.Context())
        {
            db.UserProfiles.Add(new UserProfile { UserId = user.Id, DisplayName = "Audit Chef", DietType = "vegan", AvatarUrl = avatar.Old });
            await db.SaveChangesAsync();
        }
        using var app = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAvatarStorage>();
            services.AddSingleton<IAvatarStorage>(avatar);
        }));
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var authenticated = fixture.Factory.Client(user.Id);
        client.DefaultRequestHeaders.Authorization = authenticated.DefaultRequestHeaders.Authorization;
        using var bitmap = new SKBitmap(4, 4);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        async Task<HttpStatusCode> UploadAsync(string name)
        {
            using var payload = new MultipartFormDataContent();
            var file = new ByteArrayContent(encoded.ToArray());
            file.Headers.ContentType = new("image/png");
            payload.Add(file, "avatar", name);
            using var response = await client.PutAsync("/api/users/me", payload);
            return response.StatusCode;
        }

        var responses = await Task.WhenAll(UploadAsync("first.png"), UploadAsync("second.png"));
        Assert.All(responses, status => Assert.Equal(HttpStatusCode.OK, status));

        await using var check = fixture.Context();
        var saved = (await check.UserProfiles.SingleAsync(profile => profile.UserId == user.Id)).AvatarUrl;
        Assert.Contains(saved, new[] { avatar.First, avatar.Second });
        var expectedPrevious = saved == avatar.First ? avatar.Second : avatar.First;
        Assert.Equal(2, await check.Set<AvatarCleanupItem>()
            .CountAsync(item => item.Url == avatar.Old || item.Url == expectedPrevious));
        Assert.False(await check.Set<AvatarCleanupItem>().AnyAsync(item => item.Url == saved));
    }

    private sealed class RecordingAvatarStorage : IAvatarStorage
    {
        private readonly string prefix = $"https://storage.example.invalid/avatars/{Guid.NewGuid():N}/";
        public string Old => prefix + "old.png";
        public string First => prefix + "first.png";
        public string Second => prefix + "second.png";

        public bool OwnsAvatar(string? url) => url?.StartsWith(prefix, StringComparison.Ordinal) == true;

        public Task<string> SaveAsync(byte[] content, string fileName, string contentType, CancellationToken ct) =>
            Task.FromResult(prefix + fileName);
    }

    private async Task<User> SeedUserAsync(int role = 1, string? username = null)
    {
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User { Email = suffix + "@example.invalid", Username = username ?? "audit_" + suffix,
            RoleId = role, EmailVerifiedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
