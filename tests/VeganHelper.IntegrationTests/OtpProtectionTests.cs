using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VeganHelper.BLL.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VeganHelper.BLL.Contracts;
using VeganHelper.DAL.Entities;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class OtpProtectionTests(Member2ApiFixture fixture)
{
    [Fact]
    public async Task SameCodeCanVerifyTwoAccountsIndependently()
    {
        var first = await SeedAsync("741369");
        var second = await SeedAsync("741369");
        using var client = fixture.Factory.Client();
        foreach (var email in new[] { first, second })
        {
            using var result = await client.PostAsJsonAsync("/api/auth/register/verify", new { email, otp = "741369" });
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }
    }

    [Fact]
    public async Task FiveConcurrentWrongAttemptsExhaustTheCode()
    {
        var email = await SeedAsync("741370");
        using var client = fixture.Factory.Client();
        var wrong = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            client.PostAsJsonAsync("/api/auth/register/verify", new { email, otp = "741371" })));
        foreach (var response in wrong)
        {
            using (response) Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var correct = await client.PostAsJsonAsync("/api/auth/register/verify", new { email, otp = "741370" });
        Assert.Equal(HttpStatusCode.BadRequest, correct.StatusCode);
    }

    [Fact]
    public async Task ResendDuringCooldownKeepsTheExistingCode()
    {
        var email = await SeedAsync("741372");
        using var client = fixture.Factory.Client();
        var requests = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            client.PostAsJsonAsync("/api/auth/register/start", new { email })));
        foreach (var response in requests)
        {
            using (response) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var correct = await client.PostAsJsonAsync("/api/auth/register/verify", new { email, otp = "741372" });
        Assert.Equal(HttpStatusCode.OK, correct.StatusCode);
        await using var db = fixture.Context();
        Assert.Equal(1, await db.Set<RegistrationChallenge>().CountAsync(t => t.Email == email));
    }

    [Fact]
    public async Task AValidCodeCanOnlyBeConsumedOnce()
    {
        var email = await SeedAsync("741373");
        using var client = fixture.Factory.Client();
        var confirmations = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            client.PostAsJsonAsync("/api/auth/register/verify", new { email, otp = "741373" })));
        try
        {
            Assert.Single(confirmations, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(confirmations, response => response.StatusCode == HttpStatusCode.BadRequest);
        }
        finally
        {
            foreach (var response in confirmations) response.Dispose();
        }
    }

    [Fact]
    public async Task AnOtpIssuedForAnotherPurposeCannotVerifyRegistration()
    {
        var email = await SeedAsync("741374", "google_unlink");
        using var client = fixture.Factory.Client();
        using var response = await client.PostAsJsonAsync("/api/auth/register/verify", new { email, otp = "741374" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PublicResendIsRateLimitedEvenForUnknownAccounts()
    {
        using var app = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender, SinkEmailSender>();
        }));
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        for (var i = 0; i < 12; i++)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/register/start",
                new { email = $"missing-{i}@example.invalid" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var limited = await client.PostAsJsonAsync("/api/auth/register/start",
            new { email = "missing-over-limit@example.invalid" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task PendingAccountCredentialsCanOnlyChangeWithOneTimeEmailProof()
    {
        var email = await SeedAsync("741375");
        var suffix = Guid.NewGuid().ToString("N");
        await using (var db = fixture.Context())
        {
            db.Users.Add(new User { Email = email, Username = "pending_" + suffix,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword!123"), IsActive = false, RoleId = 1, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        using var client = fixture.Factory.Client();
        using var legacy = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "AttackPass!123" });
        Assert.Equal(HttpStatusCode.Conflict, legacy.StatusCode);
        await using (var db = fixture.Context())
        {
            var pending = await db.Users.SingleAsync(u => u.Email == email);
            Assert.True(BCrypt.Net.BCrypt.Verify("OldPassword!123", pending.PasswordHash));
            Assert.False(pending.IsActive);
        }
        using var verified = await client.PostAsJsonAsync("/api/auth/register/verify", new { email, otp = "741375" });
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        var proof = (await verified.Content.ReadFromJsonAsync<RegistrationProofDto>())!.RegistrationToken;
        var completion = new { registrationToken = proof, username = "verified_" + suffix, fullName = "Verified Member",
            password = "OwnerPass!123", confirmPassword = "OwnerPass!123" };
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            client.PostAsJsonAsync("/api/auth/register/complete", completion)));
        try
        {
            Assert.Single(results, result => result.StatusCode == HttpStatusCode.Created);
            Assert.Single(results, result => result.StatusCode == HttpStatusCode.BadRequest);
        }
        finally { foreach (var result in results) result.Dispose(); }

        await using var final = fixture.Context();
        var user = await final.Users.SingleAsync(u => u.Email == email);
        Assert.True(user.IsActive);
        Assert.True(BCrypt.Net.BCrypt.Verify("OwnerPass!123", user.PasswordHash));
    }

    private sealed class SinkEmailSender : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private async Task<string> SeedAsync(string otp, string purpose = "registration")
    {
        await using var db = fixture.Context();
        var name = Guid.NewGuid().ToString("N");
        var email = name + "@example.invalid";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(otp))).ToLowerInvariant();
        if (purpose == "registration")
            db.Set<RegistrationChallenge>().Add(new RegistrationChallenge { Email = email, OtpHash = hash, CreatedAt = DateTime.UtcNow });
        else
        {
            var user = new User { Username = name, Email = email, RoleId = 1, CreatedAt = DateTime.UtcNow };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.EmailVerificationTokens.Add(new EmailVerificationToken { UserId = user.Id, TargetEmail = email,
                Purpose = purpose, TokenHash = hash, CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(15) });
        }
        await db.SaveChangesAsync();
        return email;
    }
}
