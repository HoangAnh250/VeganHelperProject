using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;
using VeganHelper.DAL.Repositories;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using Npgsql;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class AdminMemberTests(Member2ApiFixture fixture)
{
    private const string Password = "Member-test-391!";
    private async Task<User> UserAsync(int role = 1, bool active = true, bool verified = true, bool locked = false)
    {
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User { Username = "member_" + suffix, Email = suffix + "@example.invalid", RoleId = role,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4), IsActive = active,
            EmailVerifiedAt = verified ? DateTime.UtcNow : null, CreatedAt = DateTime.UtcNow,
            LockedUntil = locked ? DateTime.UtcNow.AddMinutes(15) : null };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserProfiles.Add(new UserProfile { UserId = user.Id, DisplayName = "ĐẬU %_ " + suffix, DietType = "vegan" });
        await db.SaveChangesAsync();
        return user;
    }
    private HttpClient Client(long id, string role = "admin", int? version = 0)
    {
        var client = fixture.Factory.Client();
        var claims = new List<Claim> { new("sub", id.ToString()), new(ClaimTypes.Role, role) };
        if (version.HasValue) claims.Add(new("token_version", version.Value.ToString()));
        var token = new JwtSecurityToken("member2-test", "member2-test", claims,
            expires: DateTime.UtcNow.AddHours(1), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Member2ApiFactory.Key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
    [Fact]
    public async Task ListMembers_WhenAnonymousOrMember_DeniesAccess()
    {
        using var guest = fixture.Factory.Client();
        using var member = Client((await UserAsync()).Id);

        var anonymous = await guest.GetAsync("/api/admin/members");
        var forbidden = await member.GetAsync("/api/admin/members");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
    [Fact]
    public async Task ListMembers_WhenSearchingUnicodeAndLiteralWildcards_ReturnsSafePagedDtoAndAudit()
    {
        var admin = await UserAsync(2);
        var member = await UserAsync();
        using var client = Client(admin.Id);
        var keyword = Uri.EscapeDataString("đậu %_ " + member.Email.Split('@')[0]);

        var response = await client.GetAsync($"/api/admin/members?Keyword={keyword}&Role=member&Status=active&PageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        var item = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal(member.Id, item.GetProperty("id").GetInt64());
        Assert.Equal("active", item.GetProperty("status").GetString());
        foreach (var field in new[] { "passwordHash", "tokenVersion", "pendingEmail", "weightKg" }) Assert.False(item.TryGetProperty(field, out _));
        await using var db = fixture.Context();
        Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.AdminId == admin.Id && a.Action == "member.list"));
    }
    [Theory]
    [InlineData("PageIndex=0")]
    [InlineData("PageSize=101")]
    [InlineData("Role=owner")]
    [InlineData("Status=unknown")]
    public async Task ListMembers_WhenFilterInvalid_ReturnsBadRequest(string query)
    {
        using var client = Client((await UserAsync(2)).Id);

        var response = await client.GetAsync("/api/admin/members?" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BanMember_WhenValid_RevokesAccessAndRefreshWithoutChangingActivation(bool temporary)
    {
        var admin = await UserAsync(2);
        var member = await UserAsync();
        using var client = Client(admin.Id);
        using var oldSession = Client(member.Id, "member");
        using var guest = fixture.Factory.Client();
        var login = await guest.PostAsJsonAsync("/api/auth/login", new { identifier = member.Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<JsonElement>();
        var refresh = tokens.GetProperty("refreshToken").GetString();
        DateTimeOffset? expiry = temporary ? DateTimeOffset.UtcNow.AddHours(1) : null;

        var response = await client.PostAsJsonAsync($"/api/admin/members/{member.Id}/ban", new { reason = "  Policy violation  ", expiresAt = expiry });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("banned", detail.GetProperty("status").GetString());
        Assert.Equal("Policy violation", detail.GetProperty("banReason").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldSession.GetAsync("/api/notifications")).StatusCode);
        using var currentVersion = Client(member.Id, "member", version: 1);
        Assert.Equal(HttpStatusCode.Unauthorized, (await currentVersion.GetAsync("/api/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.PostAsJsonAsync("/api/auth/login", new { identifier = member.Email, password = Password })).StatusCode);
        await using var db = fixture.Context();
        var saved = await db.Users.SingleAsync(u => u.Id == member.Id);
        Assert.True(saved.IsActive);
        Assert.Null(saved.LockedUntil);
        Assert.All(await db.RefreshTokens.Where(t => t.UserId == member.Id).ToListAsync(), t => Assert.NotNull(t.RevokedAt));
        Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.AdminId == admin.Id && a.Action == "member.ban" && a.TargetId == member.Id.ToString()));
    }
    [Fact]
    public async Task UnbanMember_WhenBanned_RequiresNewLogin()
    {
        var admin = await UserAsync(2);
        var member = await UserAsync();
        using var client = Client(admin.Id);
        using var oldSession = Client(member.Id, "member");
        await client.PostAsJsonAsync($"/api/admin/members/{member.Id}/ban", new { reason = "Review" });

        var response = await client.PostAsJsonAsync($"/api/admin/members/{member.Id}/unban", new { reason = "Appeal accepted" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("active", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldSession.GetAsync("/api/notifications")).StatusCode);
        using var guest = fixture.Factory.Client();
        var login = await guest.PostAsJsonAsync("/api/auth/login", new { identifier = member.Username, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var accessToken = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        Assert.Equal("2", jwt.Claims.Single(c => c.Type == "token_version").Value);
        guest.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/api/notifications")).StatusCode);
    }
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BanMember_WhenReasonEmpty_ReturnsBadRequest(string reason)
    {
        using var client = Client((await UserAsync(2)).Id);
        var member = await UserAsync();

        var response = await client.PostAsJsonAsync($"/api/admin/members/{member.Id}/ban", new { reason });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task BanMember_WhenExpiryPastOrTargetAdminOrMissing_DeniesMutation()
    {
        var admin = await UserAsync(2);
        var otherAdmin = await UserAsync(2);
        var member = await UserAsync();
        using var client = Client(admin.Id);

        var past = await client.PostAsJsonAsync($"/api/admin/members/{member.Id}/ban", new { reason = "Review", expiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) });
        var self = await client.PostAsJsonAsync($"/api/admin/members/{admin.Id}/ban", new { reason = "Review" });
        var other = await client.PostAsJsonAsync($"/api/admin/members/{otherAdmin.Id}/ban", new { reason = "Review" });
        var missing = await client.PostAsJsonAsync("/api/admin/members/9223372036854775807/ban", new { reason = "Review" });

        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
    [Fact]
    public async Task BanMember_WhenConcurrent_OnlyOneSucceedsAndOneAuditCommits()
    {
        var admin = await UserAsync(2);
        var member = await UserAsync();
        using var first = Client(admin.Id);
        using var second = Client(admin.Id);

        var responses = await Task.WhenAll(first.PostAsJsonAsync($"/api/admin/members/{member.Id}/ban", new { reason = "First" }),
            second.PostAsJsonAsync($"/api/admin/members/{member.Id}/ban", new { reason = "Second" }));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        await using var db = fixture.Context();
        Assert.Equal(1, await db.AdminAuditLogs.CountAsync(a => a.AdminId == admin.Id && a.Action == "member.ban"));
    }
    [Fact]
    public async Task UnbanMember_WhenNotBanned_ReturnsConflict()
    {
        using var client = Client((await UserAsync(2)).Id);
        var member = await UserAsync();

        var response = await client.PostAsJsonAsync($"/api/admin/members/{member.Id}/unban", new { reason = "Review" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
    [Fact]
    public async Task ProtectedApi_WhenTokenHasNoSessionVersion_RequiresLoginAgain()
    {
        using var client = Client((await UserAsync()).Id, "member", version: null);

        var response = await client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BanMember_WhenExpired_AllowsNewLoginButNeverRestoresOldSessions()
    {
        var admin = await UserAsync(2);
        var member = await UserAsync();
        using var client = Client(admin.Id);
        using var old = Client(member.Id, "member");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/admin/members/{member.Id}/ban", new { reason = "Temporary", expiresAt = DateTimeOffset.UtcNow.AddHours(1) })).StatusCode);
        await using (var db = fixture.Context())
            await db.UserBans.Where(b => b.UserId == member.Id).ExecuteUpdateAsync(s => s
                .SetProperty(b => b.CreatedAt, DateTime.UtcNow.AddHours(-2)).SetProperty(b => b.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/admin/members/{member.Id}");
        using var guest = fixture.Factory.Client();
        var login = await guest.PostAsJsonAsync("/api/auth/login", new { identifier = member.Email, password = Password });

        Assert.Equal("active", detail.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("banId").ValueKind);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await old.GetAsync("/api/notifications")).StatusCode);
        await using var verify = fixture.Context();
        Assert.Single(await verify.UserBans.Where(b => b.UserId == member.Id).ToListAsync());
    }

    [Fact]
    public async Task Refresh_WhenStaleTokenInsertedAfterBan_CannotRestoreSessionAfterUnban()
    {
        var admin = await UserAsync(2);
        var member = await UserAsync();
        using var client = Client(admin.Id);
        await client.PostAsJsonAsync($"/api/admin/members/{member.Id}/ban", new { reason = "Review" });
        var raw = Guid.NewGuid().ToString("N");
        await using (var db = fixture.Context())
        {
            // Represents a concurrent login that loaded the user before the ban, then saved its token after revocation.
            db.RefreshTokens.Add(new RefreshToken { UserId = member.Id, TokenVersion = 0,
                TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant(),
                CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(1) });
            await db.SaveChangesAsync();
        }
        await client.PostAsJsonAsync($"/api/admin/members/{member.Id}/unban", new { reason = "Accepted" });
        // Isolate version validation from the independent revoked_at check.
        await using (var db = fixture.Context())
            await db.RefreshTokens.Where(t => t.UserId == member.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTime?)null));
        using var guest = fixture.Factory.Client();

        var response = await guest.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = raw });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(false, false, "pending_verification")]
    [InlineData(false, true, "inactive")]
    [InlineData(true, true, "locked")]
    public async Task UnbanMember_WhenOtherRestrictionsExist_PreservesThem(bool active, bool verified, string expected)
    {
        var admin = await UserAsync(2);
        var member = await UserAsync(active: active, verified: verified, locked: expected == "locked");
        using var client = Client(admin.Id);
        await client.PostAsJsonAsync($"/api/admin/members/{member.Id}/ban", new { reason = "Review" });

        var response = await client.PostAsJsonAsync($"/api/admin/members/{member.Id}/unban", new { reason = "Accepted" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        await using var db = fixture.Context();
        var saved = await db.Users.SingleAsync(u => u.Id == member.Id);
        Assert.Equal(active, saved.IsActive);
        Assert.Equal(verified, saved.EmailVerifiedAt.HasValue);
        Assert.Equal(expected == "locked", saved.LockedUntil.HasValue);
        var history = await db.UserBans.SingleAsync(b => b.UserId == member.Id);
        Assert.Equal("Review", history.Reason);
        Assert.Equal("Accepted", history.UnbanReason);
        Assert.Equal(admin.Id, history.UnbannedByAdminId);
        Assert.NotNull(history.UnbannedAt);
    }

    [Fact]
    public async Task ListMembers_WhenFilteringBannedRoleAndEmail_ReturnsMatchingMember()
    {
        var admin = await UserAsync(2);
        var member = await UserAsync();
        using var client = Client(admin.Id);
        await client.PostAsJsonAsync($"/api/admin/members/{member.Id}/ban", new { reason = "Review" });

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/admin/members?Keyword={member.Email.ToUpperInvariant()}&Role=member&Status=banned");
        var noMatch = await client.GetFromJsonAsync<JsonElement>($"/api/admin/members?Keyword={member.Username}&Role=admin&Status=banned");

        Assert.Equal(member.Id, Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt64());
        Assert.Equal(0, noMatch.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task BanMember_WhenAuditFails_RollsBackBanVersionAndRefreshRevocation()
    {
        var admin = await UserAsync(2);
        var member = await UserAsync();
        await using (var db = fixture.Context())
        {
            db.RefreshTokens.Add(new RefreshToken { UserId = member.Id, TokenHash = Guid.NewGuid().ToString("N"), CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(1) });
            await db.SaveChangesAsync();
        }
        await using var factory = fixture.Factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IAdminAuditRepository>();
            s.AddScoped<IAdminAuditRepository, FailingAuditRepository>();
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost") });
        using var tokenClient = Client(admin.Id);
        client.DefaultRequestHeaders.Authorization = tokenClient.DefaultRequestHeaders.Authorization;

        var response = await client.PostAsJsonAsync($"/api/admin/members/{member.Id}/ban", new { reason = "Review" });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var verify = fixture.Context();
        Assert.False(await verify.UserBans.AnyAsync(b => b.UserId == member.Id));
        Assert.Equal(0, (await verify.Users.SingleAsync(u => u.Id == member.Id)).TokenVersion);
        Assert.Null((await verify.RefreshTokens.SingleAsync(t => t.UserId == member.Id)).RevokedAt);
    }

    private sealed class FailingAuditRepository(AppDbContext db) : IAdminAuditRepository
    {
        private readonly AdminAuditRepository inner = new(db);
        public Task<bool> IsActiveAdminAsync(long id, DateTime now, CancellationToken ct) => inner.IsActiveAdminAsync(id, now, ct);
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct) => inner.BeginTransactionAsync(ct);
        public Task AppendAsync(AdminAuditLog entry, CancellationToken ct) => throw new InvalidOperationException("Injected audit failure");
        public Task<DatabasePage<AdminAuditLog>> ListAsync(long? id, string? action, string? type, string? target,
            DateTime? from, DateTime? to, int index, int size, CancellationToken ct) => inner.ListAsync(id, action, type, target, from, to, index, size, ct);
        public Task<AdminAuditLog?> FindAsync(long id, CancellationToken ct) => inner.FindAsync(id, ct);
    }

    [Fact]
    public async Task GoogleLogin_WhenLinkedAccountBanned_DoesNotIssueTokens()
    {
        var admin = await UserAsync(2);
        var member = await UserAsync();
        var subject = Guid.NewGuid().ToString("N");
        await using (var db = fixture.Context())
        {
            db.UserIdentities.Add(new UserIdentity { UserId = member.Id, Provider = "google", ProviderSubject = subject, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        using var client = Client(admin.Id);
        await client.PostAsJsonAsync($"/api/admin/members/{member.Id}/ban", new { reason = "Review" });
        await using var factory = fixture.Factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IGoogleTokenValidator>();
            s.AddSingleton<IGoogleTokenValidator>(new GoogleIdentity(new(subject, member.Email, true, "Member", null)));
        }));
        using var guest = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost") });

        var response = await guest.PostAsJsonAsync("/api/auth/google", new { idToken = "test-external-identity" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var verify = fixture.Context();
        Assert.False(await verify.RefreshTokens.AnyAsync(t => t.UserId == member.Id));
    }
    private sealed class GoogleIdentity(GoogleIdentityInfo identity) : IGoogleTokenValidator
    {
        public Task<GoogleIdentityInfo?> ValidateAsync(string token, CancellationToken ct) => Task.FromResult<GoogleIdentityInfo?>(identity);
    }

    [Fact]
    public async Task Migration_WhenCreatingBanHistory_ProtectsItFromBrowserRoles()
    {
        await using var connection = new NpgsqlConnection(fixture.Connection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT c.relrowsecurity
                AND (SELECT count(*) FROM pg_roles WHERE rolname IN ('anon','authenticated')) = 2
                AND NOT EXISTS (SELECT 1 FROM aclexplode(COALESCE(c.relacl, acldefault('r', c.relowner))) a WHERE a.grantee = 0)
                AND NOT EXISTS (SELECT 1 FROM pg_roles r WHERE r.rolname IN ('anon','authenticated')
                    AND has_table_privilege(r.oid, c.oid, 'SELECT,INSERT,UPDATE,DELETE'))
                AND NOT EXISTS (SELECT 1 FROM pg_roles r WHERE r.rolname IN ('anon','authenticated')
                    AND has_sequence_privilege(r.oid, pg_get_serial_sequence('public.user_bans','id'), 'USAGE,SELECT'))
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relname = 'user_bans'
            """, connection);

        var protectedTable = await command.ExecuteScalarAsync();

        Assert.Equal(true, protectedTable);
    }

    [Fact]
    public async Task ListMembers_WhenPagingMatchingUsers_UsesStableNewestOrderAndAuditsDetail()
    {
        var admin = await UserAsync(2);
        var members = new[] { await UserAsync(), await UserAsync(), await UserAsync() };
        var group = Guid.NewGuid().ToString("N");
        var ids = members.Select(u => u.Id).ToArray();
        await using (var db = fixture.Context())
        {
            await db.UserProfiles.Where(p => ids.Contains(p.UserId)).ExecuteUpdateAsync(s => s.SetProperty(p => p.DisplayName, group));
            await db.Users.Where(u => ids.Contains(u.Id)).ExecuteUpdateAsync(s => s.SetProperty(u => u.CreatedAt, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        }
        using var client = Client(admin.Id);

        var first = await client.GetFromJsonAsync<JsonElement>($"/api/admin/members?Keyword={group}&PageSize=2");
        var second = await client.GetFromJsonAsync<JsonElement>($"/api/admin/members?Keyword={group}&PageSize=2&PageIndex=2");
        var detail = await client.GetAsync($"/api/admin/members/{members[0].Id}");
        var missing = await client.GetAsync("/api/admin/members/9223372036854775807");

        Assert.Equal(3, first.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, first.GetProperty("totalPages").GetInt32());
        Assert.Equal(new[] { members[2].Id, members[1].Id }, first.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt64()));
        Assert.Equal(members[0].Id, Assert.Single(second.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt64());
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        await using var verify = fixture.Context();
        Assert.Single(await verify.AdminAuditLogs.Where(a => a.AdminId == admin.Id && a.Action == "member.view").ToListAsync());
    }

    [Theory]
    [InlineData("pending_verification")]
    [InlineData("inactive")]
    [InlineData("locked")]
    [InlineData("deleted")]
    public async Task ListMembers_WhenFilteringRestrictionStatus_ReturnsConsistentDetail(string status)
    {
        var admin = await UserAsync(2);
        var member = await UserAsync(active: status != "inactive" && status != "deleted", verified: status != "pending_verification", locked: status == "locked");
        if (status == "deleted")
        {
            await using var db = fixture.Context();
            await db.Users.Where(u => u.Id == member.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.DeletedAt, DateTime.UtcNow));
        }
        using var client = Client(admin.Id);

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/admin/members?Keyword={member.Username.ToUpperInvariant()}&Status={status}");
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/admin/members/{member.Id}");

        Assert.Equal(member.Id, Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt64());
        Assert.Equal(status, detail.GetProperty("status").GetString());
    }
}
