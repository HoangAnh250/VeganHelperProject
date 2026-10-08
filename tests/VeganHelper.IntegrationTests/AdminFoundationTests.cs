using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs.Admin;
using VeganHelper.DAL.Entities;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class AdminFoundationTests(Member2ApiFixture fixture)
{
    [Fact]
    public async Task ListAuditLogs_WhenAnonymous_ReturnsUnauthorized()
    {
        using var client = fixture.Factory.Client();

        var response = await client.GetAsync("/api/admin/audit-logs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<long> UserAsync(int role = 2, bool active = true, bool deleted = false, bool locked = false)
    {
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User { Username = "audit_" + suffix, Email = suffix + "@example.invalid", RoleId = role,
            IsActive = active, DeletedAt = deleted ? DateTime.UtcNow : null,
            LockedUntil = locked ? DateTime.UtcNow.AddMinutes(15) : null, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private HttpClient Client(string subject, string role = "admin", bool expired = false)
    {
        var client = fixture.Factory.Client();
        var token = new JwtSecurityToken("member2-test", "member2-test",
            [new Claim("sub", subject), new Claim(ClaimTypes.Role, role), new Claim("token_version", "0")],
            notBefore: DateTime.UtcNow.AddHours(-2), expires: expired ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Member2ApiFactory.Key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    [Theory]
    [InlineData(1, true, false, false)]
    [InlineData(2, false, false, false)]
    [InlineData(2, false, true, false)]
    [InlineData(2, true, false, true)]
    public async Task ListAuditLogs_WhenDatabaseDeniesAdminDespiteClaim_ReturnsForbidden(int role, bool active, bool deleted, bool locked)
    {
        using var client = Client((await UserAsync(role, active, deleted, locked)).ToString());

        var response = await client.GetAsync("/api/admin/audit-logs");

        Assert.Equal(active && !deleted ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-user")]
    [InlineData("9223372036854775807")]
    public async Task ListAuditLogs_WhenSubjectInvalidOrMissingUser_ReturnsUnauthorized(string subject)
    {
        using var client = Client(subject);

        var response = await client.GetAsync("/api/admin/audit-logs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListAuditLogs_WhenTokenExpired_ReturnsUnauthorized()
    {
        using var client = Client((await UserAsync()).ToString(), expired: true);

        var response = await client.GetAsync("/api/admin/audit-logs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListAuditLogs_WhenRoleRemovedDuringSession_DeniesSameToken()
    {
        var admin = await UserAsync();
        using var client = Client(admin.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/audit-logs")).StatusCode);
        await using var db = fixture.Context();
        await db.Users.Where(u => u.Id == admin).ExecuteUpdateAsync(s => s.SetProperty(u => u.RoleId, 1));

        var response = await client.GetAsync("/api/admin/audit-logs");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListAuditLogs_WhenAuthorized_ReturnsPagedDtoAndAuditsReadWithoutTrustingForwardedIp()
    {
        var admin = await UserAsync();
        using var client = Client(admin.ToString());
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.199");
        var before = DateTime.UtcNow;

        var response = await client.GetAsync($"/api/admin/audit-logs?AdminId={admin}&PageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(0, body.GetProperty("totalCount").GetInt32());
        await using var db = fixture.Context();
        var entry = await db.Set<AdminAuditLog>().SingleAsync(a => a.AdminId == admin);
        Assert.Equal("audit.list", entry.Action);
        Assert.Equal("audit_log", entry.TargetType);
        Assert.InRange(entry.CreatedAt, before, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, entry.CreatedAt.Kind);
        Assert.False(string.IsNullOrWhiteSpace(entry.TraceId));
        Assert.NotEqual("203.0.113.199", entry.IpAddress);
        var next = await client.GetFromJsonAsync<JsonElement>($"/api/admin/audit-logs?AdminId={admin}&Action=audit.list&TargetType=audit_log&PageSize=1");
        var item = Assert.Single(next.GetProperty("items").EnumerateArray());
        Assert.Equal(entry.Id, item.GetProperty("id").GetInt64());
        Assert.Equal(admin, item.GetProperty("adminId").GetInt64());
        Assert.False(item.TryGetProperty("passwordHash", out _));
    }

    [Theory]
    [InlineData("PageIndex=0")]
    [InlineData("PageSize=101")]
    [InlineData("AdminId=-1")]
    [InlineData("From=2026-10-07T00:00:00Z&To=2026-10-06T00:00:00Z")]
    public async Task ListAuditLogs_WhenQueryInvalid_ReturnsBadRequestWithoutRecordingSuccess(string query)
    {
        var admin = await UserAsync();
        using var client = Client(admin.ToString());

        var response = await client.GetAsync("/api/admin/audit-logs?" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = fixture.Context();
        Assert.False(await db.Set<AdminAuditLog>().AnyAsync(a => a.AdminId == admin));
    }

    [Fact]
    public async Task GetAuditLog_WhenFound_AuditsAccessAndDoesNotExposeMutationEndpoints()
    {
        var admin = await UserAsync();
        using var client = Client(admin.ToString());
        await client.GetAsync("/api/admin/audit-logs");
        long id;
        await using (var db = fixture.Context()) id = await db.Set<AdminAuditLog>().Where(a => a.AdminId == admin).Select(a => a.Id).SingleAsync();

        var response = await client.GetAsync($"/api/admin/audit-logs/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(id, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/admin/audit-logs/9223372036854775807")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.DeleteAsync($"/api/admin/audit-logs/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PostAsJsonAsync("/api/admin/audit-logs", new { adminId = admin, action = "forged" })).StatusCode);
        await using var verify = fixture.Context();
        Assert.Equal(2, await verify.Set<AdminAuditLog>().CountAsync(a => a.AdminId == admin));
    }

    [Fact]
    public async Task ExecuteAudited_WhenOperationSucceeds_CommitsChangeAndServerAuditTogether()
    {
        var admin = await UserAsync();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VeganHelper.DAL.Persistence.AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IAdminAuditService>();
        var suffix = Guid.NewGuid().ToString("N");

        var id = await service.ExecuteAsync(new(admin, "127.0.0.1", "test-transaction"), "category.create", "category", null,
            async ct => { var category = new Category { Name = suffix, Slug = suffix, CategoryType = "post", PostCategoryKind = "recipe" };
                db.Categories.Add(category); await db.SaveChangesAsync(ct); return category.Id; },
            result => result.ToString(), default);

        await using var verify = fixture.Context();
        Assert.True(await verify.Categories.AnyAsync(c => c.Id == id));
        var audit = await verify.Set<AdminAuditLog>().SingleAsync(a => a.AdminId == admin);
        Assert.Equal("category.create", audit.Action);
        Assert.Equal(id.ToString(), audit.TargetId);
        Assert.Equal("127.0.0.1", audit.IpAddress);
        Assert.Equal("test-transaction", audit.TraceId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAudited_WhenOperationOrAuditFails_RollsBackPreviouslySavedChange(bool auditFails)
    {
        var admin = await UserAsync();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VeganHelper.DAL.Persistence.AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IAdminAuditService>();
        var suffix = Guid.NewGuid().ToString("N");

        var error = await Assert.ThrowsAnyAsync<Exception>(() => service.ExecuteAsync(new(admin, null, "rollback-test"), "category.create", "category", null,
            async ct => { db.Categories.Add(new Category { Name = suffix, Slug = suffix, CategoryType = "post", PostCategoryKind = "recipe" });
                await db.SaveChangesAsync(ct); if (!auditFails) throw new InvalidOperationException("operation failed");
                await db.Users.Where(u => u.Id == admin).ExecuteDeleteAsync(ct); return 1; },
            _ => "1", default));

        await using var verify = fixture.Context();
        Assert.False(await verify.Categories.AnyAsync(c => c.Slug == suffix));
        Assert.False(await verify.Set<AdminAuditLog>().AnyAsync(a => a.AdminId == admin));
        Assert.True(await verify.Users.AnyAsync(u => u.Id == admin));
        if (auditFails)
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(Assert.IsType<DbUpdateException>(error).InnerException).SqlState);
        else
            Assert.IsType<InvalidOperationException>(error);
    }

    [Fact]
    public async Task ExecuteAudited_WhenMemberCallsServiceDirectly_DeniesBeforeOperation()
    {
        var member = await UserAsync(role: 1);
        using var scope = fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAdminAuditService>();
        var ran = false;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ExecuteAsync<int>(new(member, null, "member"), "category.create", "category", null,
            _ => { ran = true; return Task.FromResult(1); }, null, default));

        Assert.False(ran);
    }

    [Theory]
    [InlineData("UPDATE public.admin_audit_logs SET action = 'tampered' WHERE admin_id = {0}")]
    [InlineData("DELETE FROM public.admin_audit_logs WHERE admin_id = {0}")]
    [InlineData("TRUNCATE public.admin_audit_logs")]
    public async Task AuditTable_WhenSqlTriesToMutateExistingLogs_RejectsMutation(string sql)
    {
        var admin = await UserAsync();
        using var client = Client(admin.ToString());
        await client.GetAsync("/api/admin/audit-logs");
        await using var db = fixture.Context();

        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(string.Format(System.Globalization.CultureInfo.InvariantCulture, sql, admin)));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        Assert.Equal("audit.list", (await db.Set<AdminAuditLog>().SingleAsync(a => a.AdminId == admin)).Action);
    }

    [Fact]
    public async Task Migration_WhenCreatingAuditTable_EnablesRlsAndRevokesDataApiAccess()
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
                AND has_sequence_privilege(r.oid, pg_get_serial_sequence('public.admin_audit_logs', 'id'), 'USAGE,SELECT'))
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relname = 'admin_audit_logs'
            """, connection);

        var protectedTable = await command.ExecuteScalarAsync();

        Assert.Equal(true, protectedTable);
    }

    [Fact]
    public async Task ListAuditLogs_WhenLoginLockExpired_UsesCurrentDatabaseRoleDespiteOldMemberClaim()
    {
        var admin = await UserAsync();
        await using var db = fixture.Context();
        await db.Users.Where(u => u.Id == admin).ExecuteUpdateAsync(s => s.SetProperty(u => u.LockedUntil, DateTime.UtcNow.AddMinutes(-1)));
        using var client = Client(admin.ToString(), role: "member");

        var response = await client.GetAsync("/api/admin/audit-logs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ListAuditLogs_WhenFilteringTargetAndTime_ReturnsOnlyMatchingEntriesInNewestOrder()
    {
        var admin = await UserAsync();
        var start = DateTime.UtcNow;
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IAdminAuditService>();
            for (var i = 1; i <= 3; i++)
                await service.ExecuteAsync(new(admin, "::1", "filter-test"), "category.update", "category", "42",
                    _ => Task.FromResult(i), null, default);
            await service.ExecuteAsync(new(admin, "::1", "filter-test"), "category.update", "category", "43",
                _ => Task.FromResult(0), null, default);
        }
        using var client = Client(admin.ToString());
        var from = Uri.EscapeDataString(start.ToString("O"));
        var to = Uri.EscapeDataString(DateTime.UtcNow.ToString("O"));

        var page1 = await client.GetFromJsonAsync<JsonElement>($"/api/admin/audit-logs?AdminId={admin}&Action=category.update&TargetType=category&TargetId=42&From={from}&To={to}&PageSize=2");
        var page2 = await client.GetFromJsonAsync<JsonElement>($"/api/admin/audit-logs?AdminId={admin}&Action=category.update&TargetType=category&TargetId=42&From={from}&To={to}&PageIndex=2&PageSize=2");

        Assert.Equal(3, page1.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page1.GetProperty("totalPages").GetInt32());
        var firstIds = page1.GetProperty("items").EnumerateArray().Select(a => a.GetProperty("id").GetInt64()).ToArray();
        Assert.Equal(2, firstIds.Length);
        var lastId = Assert.Single(page2.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt64();
        Assert.True(firstIds[0] > firstIds[1] && firstIds[1] > lastId);
    }

    [Fact]
    public async Task ExecuteAudited_WhenCancelledAfterSave_RollsBackMutationAndAudit()
    {
        var admin = await UserAsync();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VeganHelper.DAL.Persistence.AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IAdminAuditService>();
        using var cancellation = new CancellationTokenSource();
        var suffix = Guid.NewGuid().ToString("N");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExecuteAsync(new(admin, null, "cancel-test"), "category.create", "category", null,
            async ct => { db.Categories.Add(new Category { Name = suffix, Slug = suffix, CategoryType = "post", PostCategoryKind = "recipe" });
                await db.SaveChangesAsync(ct); cancellation.Cancel(); ct.ThrowIfCancellationRequested(); return 1; }, null, cancellation.Token));

        await using var verify = fixture.Context();
        Assert.False(await verify.Categories.AnyAsync(c => c.Slug == suffix));
        Assert.False(await verify.Set<AdminAuditLog>().AnyAsync(a => a.AdminId == admin));
    }

    [Fact]
    public async Task ExecuteAudited_WhenNestedTransactionRequested_RejectsBeforeCallback()
    {
        var admin = await UserAsync();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VeganHelper.DAL.Persistence.AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IAdminAuditService>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var ran = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(new(admin, null, "nested-test"), "category.create", "category", null,
            _ => { ran = true; return Task.FromResult(1); }, null, default));

        Assert.False(ran);
    }
}
