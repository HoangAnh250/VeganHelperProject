using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class PostReportTests(Member2ApiFixture fixture)
{
    private HttpClient AdminClient(long id)
    {
        var client = fixture.Factory.Client();
        var token = new JwtSecurityToken("member2-test", "member2-test", [new("sub", id.ToString()), new(ClaimTypes.Role, "admin"), new("token_version", "0")],
            expires: DateTime.UtcNow.AddHours(1), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Member2ApiFactory.Key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token)); return client;
    }
    private async Task<long> UserAsync(int role = 1)
    {
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User { Username = "report_" + suffix, Email = suffix + "@example.invalid", RoleId = role,
            IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user); await db.SaveChangesAsync();
        db.UserProfiles.Add(new UserProfile { UserId = user.Id, DisplayName = user.Username, DietType = "vegan" });
        await db.SaveChangesAsync(); return user.Id;
    }
    private async Task<Post> PostAsync(long author)
    {
        await using var db = fixture.Context();
        var post = new Post { AuthorId = author, Title = "Original tofu recipe", Content = "Reported content",
            PostType = "article", Status = "published", CreatedAt = DateTime.UtcNow };
        db.Posts.Add(post); await db.SaveChangesAsync(); return post;
    }
    [Fact]
    public async Task PrivateProfile_ReturnsDatabaseRoleForAdminRoutes()
    {
        var admin = await UserAsync(2); using var client = fixture.Factory.Client(admin);
        var response = await client.GetFromJsonAsync<JsonElement>("/api/users/me");
        Assert.Equal("admin", response.GetProperty("role").GetString());
    }

    [Theory]
    [InlineData("dismiss", "pending_review", true)]
    [InlineData("keep", "published", false)]
    [InlineData("remove", "hidden", true)]
    public async Task AdminDecision_RespectsHoldAndAudits(string action, string status, bool hold)
    {
        var post = await PostAsync(await UserAsync()); using var reporter = fixture.Factory.Client(await UserAsync());
        var received = await (await reporter.PostAsJsonAsync($"/api/posts/{post.Id}/reports", new { reasonCategory = "non_vegan", reason = "Image", expectedRevision = 1 }))
            .Content.ReadFromJsonAsync<JsonElement>(); var id = received.GetProperty("id").GetInt64();
        await using (var db = fixture.Context())
            await db.Posts.Where(p => p.Id == post.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.ModerationHold, true).SetProperty(p => p.Status, "pending_review"));
        var adminId = await UserAsync(2); using var admin = AdminClient(adminId);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/admin/moderation/reports/{id}/decision", new { action, reason = "Reviewed current version", expectedRevision = 1 })).StatusCode);
        await using var verify = fixture.Context();
        var saved = await verify.Posts.SingleAsync(p => p.Id == post.Id); Assert.Equal(status, saved.Status); Assert.Equal(hold, saved.ModerationHold);
        Assert.True(await verify.AdminAuditLogs.AnyAsync(a => a.AdminId == adminId && a.Action == "report." + action));
        Assert.Equal(action != "dismiss", await verify.Set<Notification>().AnyAsync(n => n.UserId == post.AuthorId));
    }

    [Fact]
    public async Task DeletedReportedPost_CanBeViewedAndDismissedButNeverRestored()
    {
        var post = await PostAsync(await UserAsync()); using var reporter = fixture.Factory.Client(await UserAsync());
        var response = await (await reporter.PostAsJsonAsync($"/api/posts/{post.Id}/reports", new { reasonCategory = "other", reason = "Needs review", expectedRevision = 1 }))
            .Content.ReadFromJsonAsync<JsonElement>(); var id = response.GetProperty("id").GetInt64();
        await using (var db = fixture.Context())
            await db.Posts.Where(p => p.Id == post.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDeleted, true)
                .SetProperty(p => p.DeletedAt, DateTime.UtcNow).SetProperty(p => p.ContentRevision, 2));
        using var admin = AdminClient(await UserAsync(2));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/admin/moderation/reports/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/admin/moderation/reports/{id}/decision", new { action = "keep", reason = "Restore", expectedRevision = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/admin/moderation/reports/{id}/decision", new { action = "dismiss", reason = "Author deleted post", expectedRevision = 2 })).StatusCode);
    }

    [Fact]
    public async Task AdminDecision_RejectsStaleRevisionAndCommitsOnceUnderConcurrency()
    {
        var post = await PostAsync(await UserAsync()); using var reporter = fixture.Factory.Client(await UserAsync());
        var receipt = await (await reporter.PostAsJsonAsync($"/api/posts/{post.Id}/reports", new { reasonCategory = "spam", reason = "Suspicious advertising", expectedRevision = 1 }))
            .Content.ReadFromJsonAsync<JsonElement>(); var id = receipt.GetProperty("id").GetInt64();
        var adminId = await UserAsync(2); using var admin = AdminClient(adminId);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/admin/moderation/reports/{id}/decision",
            new { action = "remove", reason = "Review", expectedRevision = 2 })).StatusCode);
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => admin.PostAsJsonAsync($"/api/admin/moderation/reports/{id}/decision",
            new { action = "remove", reason = "Review", expectedRevision = 1 })));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        await using var db = fixture.Context();
        Assert.Single(await db.Set<PostModerationDecision>().Where(d => d.PostId == post.Id).ToListAsync());
        Assert.Single(await db.Set<Notification>().Where(n => n.UserId == post.AuthorId).ToListAsync());
    }

    [Fact]
    public async Task Report_RechecksSafeRevisionOnceAndPreservesAssessmentProvenance()
    {
        var post = await PostAsync(await UserAsync());
        await using (var db = fixture.Context())
        {
            db.Add(new PostModerationScan { PostId = post.Id, Revision = 1, State = "safe", Attempts = 1 });
            db.Add(new PostModerationAssessment { PostId = post.Id, Revision = 1, RequestId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, ResultJson = "{\"state\":\"safe\"}" });
            await db.SaveChangesAsync();
        }
        using var first = fixture.Factory.Client(await UserAsync()); using var second = fixture.Factory.Client(await UserAsync());
        var body = new { expectedRevision = 1, reasonCategory = "other", reason = "Check again" };
        Assert.Equal(HttpStatusCode.OK, (await first.PostAsJsonAsync($"/api/posts/{post.Id}/reports", body)).StatusCode);
        await using (var db = fixture.Context())
        {
            var scan = await db.Set<PostModerationScan>().SingleAsync(s => s.PostId == post.Id);
            Assert.Equal("queued", scan.State); Assert.Equal(1, scan.Priority);
            scan.State = "failed"; scan.Attempts = 5; await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await second.PostAsJsonAsync($"/api/posts/{post.Id}/reports", body)).StatusCode);
        await using var verify = fixture.Context();
        var final = await verify.Set<PostModerationScan>().SingleAsync(s => s.PostId == post.Id);
        Assert.Equal("failed", final.State); Assert.Equal(5, final.Attempts);
        var flag = await verify.Flags.FirstAsync(f => f.PostId == post.Id && f.SourceType == "user");
        using var admin = AdminClient(await UserAsync(2));
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/moderation/reports/{flag.Id}");
        var history = Assert.Single(detail.GetProperty("assessmentHistory").EnumerateArray());
        Assert.Equal(1, history.GetProperty("revision").GetInt32()); Assert.True(history.TryGetProperty("createdAt", out _));
    }

    [Theory]
    [InlineData("/api/admin/moderation/reports")]
    [InlineData("/api/admin/moderation/reports/1")]
    public async Task Reports_AdminEndpointsDenyGuestAndMember(string path)
    {
        using var guest = fixture.Factory.Client(); using var member = AdminClient(await UserAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Reports_EnforceRateLimitAcrossDifferentPosts()
    {
        var author = await UserAsync(); using var reporter = fixture.Factory.Client(await UserAsync());
        for (var index = 0; index < 11; index++)
        {
            var post = await PostAsync(author);
            var response = await reporter.PostAsJsonAsync($"/api/posts/{post.Id}/reports", new { expectedRevision = 1, reasonCategory = "spam", reason = "Repeated advertising" });
            Assert.Equal(index < 10 ? HttpStatusCode.OK : HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    [Fact]
    public async Task Report_DeduplicatesConcurrentRequestsAndKeepsPostPublic()
    {
        var author = await UserAsync(); var reporter = await UserAsync(); var post = await PostAsync(author);
        using var client = fixture.Factory.Client(reporter);
        var payload = new { reasonCategory = "non_vegan", reason = "Suspected meat in image", expectedRevision = 1 };
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsJsonAsync($"/api/posts/{post.Id}/reports", payload)));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        await using var db = fixture.Context();
        var flag = Assert.Single(await db.Flags.Where(f => f.PostId == post.Id).ToListAsync());
        Assert.Equal(reporter, flag.ReporterId);
        Assert.Equal("published", (await db.Posts.SingleAsync(p => p.Id == post.Id)).Status);
        Assert.False(await db.Set<Notification>().AnyAsync(n => n.UserId == author));
        var response = await responses[0].Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(response.TryGetProperty("reporterId", out _));
    }
    [Fact]
    public async Task Report_RejectsGuestAuthorStaleRevisionAndInvalidReason()
    {
        var post = await PostAsync(await UserAsync());
        using var guest = fixture.Factory.Client(); using var author = fixture.Factory.Client(post.AuthorId);
        using var reporter = fixture.Factory.Client(await UserAsync());
        var valid = new { reasonCategory = "non_vegan", reason = "Image needs review", expectedRevision = 1 };
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync($"/api/posts/{post.Id}/reports", valid)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await author.PostAsJsonAsync($"/api/posts/{post.Id}/reports", valid)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await reporter.PostAsJsonAsync($"/api/posts/{post.Id}/reports", valid with { expectedRevision = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await reporter.PostAsJsonAsync($"/api/posts/{post.Id}/reports", valid with { reasonCategory = "invalid" })).StatusCode);
    }
}
