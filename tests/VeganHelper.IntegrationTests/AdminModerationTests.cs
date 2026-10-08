using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using AutoMapper;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using VeganHelper.BLL.DTOs.Admin;
using VeganHelper.BLL.Mapping;
using VeganHelper.BLL.Services;
using VeganHelper.DAL.Persistence;
using VeganHelper.DAL.Repositories;
using VeganHelper.DAL.Entities;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class AdminModerationTests(Member2ApiFixture fixture)
{
    private async Task<long> UserAsync(int role = 2)
    {
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User { Username = "moderation_" + suffix, Email = suffix + "@example.invalid", RoleId = role, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user); await db.SaveChangesAsync(); return user.Id;
    }
    private HttpClient Client(long id)
    {
        var client = fixture.Factory.Client();
        var token = new JwtSecurityToken("member2-test", "member2-test",
            [new("sub", id.ToString()), new(ClaimTypes.Role, "admin"), new("token_version", "0")], expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Member2ApiFactory.Key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token)); return client;
    }
    private async Task<Post> PostAsync(string status = "pending_review")
    {
        var author = await UserAsync(1);
        await using var db = fixture.Context();
        var post = new Post { AuthorId = author, Title = "Đậu " + Guid.NewGuid().ToString("N"), Content = "Plant-based recipe", PostType = "recipe", Status = status, CreatedAt = DateTime.UtcNow };
        db.Posts.Add(post); await db.SaveChangesAsync(); return post;
    }
    private async Task<long> FlagAsync(Post post)
    {
        await using var db = fixture.Context();
        var flag = new Flag { PostId = post.Id, SourceType = "ai", AiModelName = "test-gemini", Reason = "Suspected animal meat in image", Status = "pending", CreatedAt = DateTime.UtcNow };
        db.Flags.Add(flag); await db.SaveChangesAsync(); return flag.Id;
    }

    [Fact]
    public async Task Moderation_WhenGuestOrMember_DeniesAccess()
    {
        using var guest = fixture.Factory.Client(); using var member = Client(await UserAsync(1));

        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/admin/moderation/posts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/admin/moderation/posts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/admin/moderation/ai-flags")).StatusCode);
    }
    [Theory]
    [InlineData("approve", "published")]
    [InlineData("reject", "rejected")]
    public async Task DecidePost_WhenPending_CommitsStatusAuditAndAuthorNotification(string action, string status)
    {
        var admin = await UserAsync(); var post = await PostAsync(); using var client = Client(admin);

        var response = await client.PostAsJsonAsync($"/api/admin/moderation/posts/{post.Id}/decision", new { action, reason = "  Review completed  ", expectedRevision = 1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.Context();
        Assert.Equal(status, (await db.Posts.SingleAsync(p => p.Id == post.Id)).Status);
        Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.AdminId == admin && a.Action == "post." + action));
        var notification = await db.Set<Notification>().SingleAsync(n => n.UserId == post.AuthorId);
        Assert.Equal("post_review", notification.Type);
        Assert.Contains("Review completed", notification.Message);
    }
    [Theory]
    [InlineData("keep", "published", "dismissed")]
    [InlineData("remove", "hidden", "resolved")]
    public async Task DecideFlag_WhenPending_RecordsHumanVerdictAndPreservesPost(string action, string status, string flagStatus)
    {
        var admin = await UserAsync(); var post = await PostAsync(); var flag = await FlagAsync(post); using var client = Client(admin);

        var response = await client.PostAsJsonAsync($"/api/admin/moderation/ai-flags/{flag}/decision", new { action, reason = "Manual review", expectedRevision = 1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.Context();
        var saved = await db.Posts.SingleAsync(p => p.Id == post.Id);
        Assert.Equal(status, saved.Status); Assert.False(saved.IsDeleted);
        var resolved = await db.Flags.SingleAsync(f => f.Id == flag);
        Assert.Equal(flagStatus, resolved.Status); Assert.Equal(admin, resolved.ResolvedByAdminId); Assert.Equal("Manual review", resolved.ResolutionNote);
        Assert.Single(await db.Set<Notification>().Where(n => n.UserId == post.AuthorId).ToListAsync());
    }
    [Fact]
    public async Task DecidePost_WhenConcurrent_OnlyOneDecisionAndNotificationCommits()
    {
        var admin = await UserAsync(); var post = await PostAsync(); using var a = Client(admin); using var b = Client(admin);

        var responses = await Task.WhenAll(a.PostAsJsonAsync($"/api/admin/moderation/posts/{post.Id}/decision", new { action = "approve", reason = "Approved", expectedRevision = 1 }),
            b.PostAsJsonAsync($"/api/admin/moderation/posts/{post.Id}/decision", new { action = "reject", reason = "Rejected", expectedRevision = 1 }));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        await using var db = fixture.Context(); Assert.Single(await db.Set<Notification>().Where(n => n.UserId == post.AuthorId).ToListAsync());
        Assert.Single(await db.AdminAuditLogs.Where(a => a.AdminId == admin).ToListAsync());
    }
    [Theory]
    [InlineData("approve", "", 1, HttpStatusCode.BadRequest)]
    [InlineData("delete", "Reason", 1, HttpStatusCode.BadRequest)]
    [InlineData("reject", "Reason", 2, HttpStatusCode.Conflict)]
    public async Task DecidePost_WhenInvalidOrStale_DoesNotMutate(string action, string reason, int revision, HttpStatusCode expected)
    {
        var admin = await UserAsync(); var post = await PostAsync(); using var client = Client(admin);

        var response = await client.PostAsJsonAsync($"/api/admin/moderation/posts/{post.Id}/decision", new { action, reason, expectedRevision = revision });

        Assert.Equal(expected, response.StatusCode); await using var db = fixture.Context();
        Assert.Equal("pending_review", (await db.Posts.SingleAsync(p => p.Id == post.Id)).Status); Assert.False(await db.AdminAuditLogs.AnyAsync(a => a.AdminId == admin));
    }
    [Fact]
    public async Task ModerationLists_WhenFiltered_ReturnPendingPostsAndFlagEvidence()
    {
        var admin = await UserAsync(); var post = await PostAsync(); var flag = await FlagAsync(post); using var client = Client(admin);

        var posts = await client.GetFromJsonAsync<JsonElement>($"/api/admin/moderation/posts?Keyword={Uri.EscapeDataString(post.Title)}&PageSize=1");
        var flags = await client.GetFromJsonAsync<JsonElement>($"/api/admin/moderation/ai-flags?PostId={post.Id}");
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/admin/moderation/posts/{post.Id}");

        Assert.Equal(post.Id, Assert.Single(posts.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt64());
        Assert.Equal(flag, Assert.Single(flags.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt64());
        Assert.Equal(1, detail.GetProperty("contentRevision").GetInt32()); Assert.False(detail.TryGetProperty("passwordHash", out _));
    }
    [Fact]
    public async Task ModerationSettings_WhenUpdated_UsesOptimisticVersionAndAuditsToggle()
    {
        var admin = await UserAsync(); using var client = Client(admin);
        var before = await client.GetFromJsonAsync<JsonElement>("/api/admin/moderation/settings"); var version = before.GetProperty("version").GetInt32();

        var update = await client.PutAsJsonAsync("/api/admin/moderation/settings", new { autoPublishEnabled = true, expectedVersion = version });
        var stale = await client.PutAsJsonAsync("/api/admin/moderation/settings", new { autoPublishEnabled = false, expectedVersion = version });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        // Restore default so tests remain independent of execution order.
        await client.PutAsJsonAsync("/api/admin/moderation/settings", new { autoPublishEnabled = false, expectedVersion = version + 1 });
    }

    [Theory]
    [InlineData("pending_review")]
    [InlineData("rejected")]
    [InlineData("hidden")]
    [InlineData("draft")]
    public async Task PostDetail_WhenUnpublished_OnlyAuthorOrAdminModerationCanRead(string status)
    {
        var post = await PostAsync(status);
        using var guest = fixture.Factory.Client(); using var other = fixture.Factory.Client(await UserAsync(1));
        using var owner = fixture.Factory.Client(post.AuthorId); using var admin = Client(await UserAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/posts/{post.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/posts/{post.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/posts/{post.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/admin/moderation/posts/{post.Id}")).StatusCode);
        await using var db = fixture.Context();
        Assert.Equal(1, (await db.Posts.SingleAsync(p => p.Id == post.Id)).ViewCount);
    }
    [Fact]
    public async Task PostDetail_WhenPublished_GuestCanRead()
    {
        var post = await PostAsync("published"); using var guest = fixture.Factory.Client();
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"/api/posts/{post.Id}")).StatusCode);
    }
    [Theory]
    [InlineData("GET", "posts")]
    [InlineData("GET", "posts/1")]
    [InlineData("GET", "ai-flags")]
    [InlineData("POST", "posts/1/decision")]
    [InlineData("POST", "ai-flags/1/decision")]
    [InlineData("GET", "settings")]
    [InlineData("PUT", "settings")]
    public async Task AllModerationRoutes_WhenMember_DenyForgedAdminClaim(string method, string path)
    {
        using var client = Client(await UserAsync(1));
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/admin/moderation/" + path)
        { Content = method == "GET" ? null : JsonContent.Create(new { action = "approve", reason = "Reason", expectedRevision = 1, expectedVersion = 1 }) };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
    }
    [Fact]
    public async Task FlagDecision_WhenAlreadyResolvedOrPreviousRevision_DoesNotRepeatNotification()
    {
        var post = await PostAsync(); var flagId = await FlagAsync(post); using var admin = Client(await UserAsync());
        var path = $"/api/admin/moderation/ai-flags/{flagId}/decision";
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync(path, new { action = "keep", reason = "Allowed", expectedRevision = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(path, new { action = "remove", reason = "Repeat", expectedRevision = 1 })).StatusCode);
        await using var db = fixture.Context(); Assert.Equal(1, await db.Set<Notification>().CountAsync(n => n.UserId == post.AuthorId));
        var stale = await FlagAsync(post);
        await db.Flags.Where(f => f.Id == stale).ExecuteUpdateAsync(s => s.SetProperty(f => f.PostRevision, 1));
        await db.Posts.Where(p => p.Id == post.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.ContentRevision, 2).SetProperty(p => p.Status, "pending_review"));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/admin/moderation/ai-flags/{stale}/decision",
            new { action = "keep", reason = "Stale flag", expectedRevision = 2 })).StatusCode);
        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/moderation/ai-flags?PostId={post.Id}");
        Assert.Empty(list.GetProperty("items").EnumerateArray());
    }
    [Theory]
    [InlineData("post_moderation_scans")]
    [InlineData("post_moderation_decisions")]
    [InlineData("post_moderation_settings")]
    public async Task ModerationTables_WhenMigrated_BlockBrowserRoles(string table)
    {
        await using var connection = new NpgsqlConnection(fixture.Connection); await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT c.relrowsecurity AND NOT EXISTS
              (SELECT 1 FROM pg_roles r WHERE r.rolname IN ('anon','authenticated')
                AND has_table_privilege(r.oid,c.oid,'SELECT,INSERT,UPDATE,DELETE'))
              AND NOT EXISTS (SELECT 1 FROM aclexplode(COALESCE(c.relacl,acldefault('r',c.relowner))) a WHERE a.grantee=0)
              AND (pg_get_serial_sequence('public.' || c.relname,'id') IS NULL OR NOT EXISTS
                (SELECT 1 FROM pg_roles r WHERE r.rolname IN ('anon','authenticated')
                  AND has_sequence_privilege(r.oid,pg_get_serial_sequence('public.' || c.relname,'id'),'USAGE,SELECT')))
            FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public' AND c.relname=@table
            """, connection);
        command.Parameters.AddWithValue("table", table);
        Assert.Equal(true, await command.ExecuteScalarAsync());
    }
    [Theory]
    [InlineData("UPDATE post_moderation_decisions SET reason='tampered' WHERE post_id={0}")]
    [InlineData("DELETE FROM post_moderation_decisions WHERE post_id={0}")]
    [InlineData("TRUNCATE post_moderation_decisions")]
    public async Task ModerationDecisions_WhenSqlMutatesHistory_RejectsMutation(string sql)
    {
        var post = await PostAsync(); using var admin = Client(await UserAsync());
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/admin/moderation/posts/{post.Id}/decision",
            new { action = "approve", reason = "Reviewed", expectedRevision = 1 })).StatusCode);
        await using var db = fixture.Context();
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(string.Format(System.Globalization.CultureInfo.InvariantCulture, sql, post.Id)));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
        Assert.Equal("Reviewed", (await db.Set<PostModerationDecision>().SingleAsync(d => d.PostId == post.Id)).Reason);
    }
    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task Decision_WhenAuditInsertFails_RollsBackStatusFlagsScanDecisionAndNotification(string action)
    {
        var admin = await UserAsync(); var post = await PostAsync(); var flagId = await FlagAsync(post);
        await using (var setup = fixture.Context())
        {
            setup.Add(new PostModerationScan { PostId = post.Id, Revision = 1 }); await setup.SaveChangesAsync();
        }
        var failure = new FailAuditInsert();
        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.Connection).AddInterceptors(failure).Options))
        {
            var mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
            var audit = new AdminAuditService(new AdminAuditRepository(db), mapper, TimeProvider.System);
            var notifications = new NotificationService(new NotificationRepository(db), mapper, TimeProvider.System);
            var service = new AdminModerationService(new ModerationRepository(db), audit, notifications, mapper, TimeProvider.System);
            await Assert.ThrowsAnyAsync<Exception>(() => service.DecideAsync(new(admin, null, "audit-failure"), post.Id,
                new ModerationDecisionRequest { Action = action, Reason = "Human review", ExpectedRevision = 1 }, default));
        }
        Assert.True(failure.Triggered, "Must reach the real audit INSERT after notification persistence.");
        await using var verify = fixture.Context();
        Assert.Equal("pending_review", (await verify.Posts.SingleAsync(p => p.Id == post.Id)).Status);
        var flag = await verify.Flags.SingleAsync(f => f.Id == flagId);
        Assert.Equal("pending", flag.Status); Assert.Null(flag.ResolvedByAdminId);
        Assert.Equal("queued", (await verify.Set<PostModerationScan>().SingleAsync(s => s.PostId == post.Id)).State);
        Assert.False(await verify.Set<PostModerationDecision>().AnyAsync(d => d.PostId == post.Id));
        Assert.False(await verify.Set<Notification>().AnyAsync(n => n.UserId == post.AuthorId));
        Assert.False(await verify.AdminAuditLogs.AnyAsync(a => a.AdminId == admin));
    }
    private sealed class FailAuditInsert : DbCommandInterceptor
    {
        public bool Triggered { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (command.CommandText.Contains("INSERT INTO admin_audit_logs", StringComparison.OrdinalIgnoreCase))
            { Triggered = true; throw new InvalidOperationException("Injected audit persistence failure"); }
            return base.ReaderExecutingAsync(command, eventData, result, ct);
        }
    }
}
