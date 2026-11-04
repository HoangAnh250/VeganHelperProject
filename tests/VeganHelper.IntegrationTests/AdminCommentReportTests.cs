using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Microsoft.IdentityModel.Tokens;
using VeganHelper.DAL.Entities;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class AdminCommentReportTests(Member2ApiFixture fixture)
{
    private async Task<User> UserAsync(int role = 1)
    {
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User { Username = "comment_report_" + suffix, Email = suffix + "@example.invalid",
            RoleId = role, IsActive = true, EmailVerifiedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserProfiles.Add(new UserProfile { UserId = user.Id, DisplayName = "User " + suffix, DietType = "vegan" });
        await db.SaveChangesAsync();
        return user;
    }

    private HttpClient Client(long id, string role = "admin")
    {
        var client = fixture.Factory.Client();
        var token = new JwtSecurityToken("member2-test", "member2-test",
            [new Claim("sub", id.ToString()), new Claim(ClaimTypes.Role, role), new Claim("token_version", "0")],
            expires: DateTime.UtcNow.AddHours(1), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Member2ApiFactory.Key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private async Task<(Comment Comment, Flag First, Flag Second)> ReportedCommentAsync(bool hidden = true)
    {
        var author = await UserAsync();
        var firstReporter = await UserAsync();
        var secondReporter = await UserAsync();
        await using var db = fixture.Context();
        var post = new Post { AuthorId = author.Id, Title = "Reported discussion", Content = "Post content",
             Status = "published", CreatedAt = DateTime.UtcNow };
        db.Posts.Add(post);
        await db.SaveChangesAsync();
        var comment = new Comment { PostId = post.Id, UserId = author.Id, Content = "Reported comment content",
            Status = hidden ? "hidden" : "visible", CreatedAt = DateTime.UtcNow };
        db.Comments.Add(comment);
        await db.SaveChangesAsync();
        var first = new Flag { CommentId = comment.Id, ReporterId = firstReporter.Id, SourceType = "user",
            Status = "pending", Reason = "Spam link", CreatedAt = DateTime.UtcNow.AddMinutes(-1) };
        var second = new Flag { CommentId = comment.Id, ReporterId = secondReporter.Id, SourceType = "user",
            Status = "pending", Reason = "Harassment", CreatedAt = DateTime.UtcNow };
        db.Flags.AddRange(first, second);
        await db.SaveChangesAsync();
        return (comment, first, second);
    }

    [Fact]
    public async Task CommentReportQueue_GroupsReportsAndRequiresAdmin()
    {
        var admin = await UserAsync(2);
        var member = await UserAsync();
        var reported = await ReportedCommentAsync();
        using var guest = fixture.Factory.Client();
        await using var db = fixture.Context();
        await db.UserProfiles.Where(profile => profile.UserId == reported.First.ReporterId)
            .ExecuteUpdateAsync(update => update.SetProperty(profile => profile.DisplayName, ""));
        var reporterUsername = await db.Users.Where(user => user.Id == reported.First.ReporterId)
            .Select(user => user.Username).SingleAsync();
        using var memberClient = Client(member.Id, "member");
        using var adminClient = Client(admin.Id);

        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/admin/moderation/comment-reports")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.GetAsync("/api/admin/moderation/comment-reports")).StatusCode);
        var response = await adminClient.GetFromJsonAsync<JsonElement>("/api/admin/moderation/comment-reports?Status=pending");

        var item = Assert.Single(response.GetProperty("items").EnumerateArray(),
            value => value.GetProperty("commentId").GetInt64() == reported.Comment.Id);
        Assert.Equal(2, item.GetProperty("reportCount").GetInt32());
        Assert.Equal("Reported comment content", item.GetProperty("commentContent").GetString());
        Assert.Equal(reporterUsername, item.GetProperty("reporterName").GetString());
    }

    [Fact]
    public async Task KeepComment_DismissesEveryPendingReportAndRestoresVisibility()
    {
        var admin = await UserAsync(2);
        var reported = await ReportedCommentAsync();
        using var client = Client(admin.Id);

        var response = await client.PostAsJsonAsync($"/api/admin/moderation/comment-reports/{reported.First.Id}/decision",
            new { action = "keep", reason = "The comment follows the community guidelines." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.Context();
        Assert.Equal("visible", (await db.Comments.SingleAsync(comment => comment.Id == reported.Comment.Id)).Status);
        Assert.All(await db.Flags.Where(flag => flag.CommentId == reported.Comment.Id).ToListAsync(), flag => Assert.Equal("dismissed", flag.Status));
        Assert.True(await db.AdminAuditLogs.AnyAsync(log => log.AdminId == admin.Id && log.Action == "comment_report.keep"));
    }

    [Fact]
    public async Task RemoveComment_ResolvesReportsAndSoftDeletesComment()
    {
        var admin = await UserAsync(2);
        var reported = await ReportedCommentAsync(hidden: false);
        using var client = Client(admin.Id);

        var response = await client.PostAsJsonAsync($"/api/admin/moderation/comment-reports/{reported.First.Id}/decision",
            new { action = "remove", reason = "The reported content violates the community guidelines." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.Context();
        var comment = await db.Comments.SingleAsync(value => value.Id == reported.Comment.Id);
        Assert.True(comment.IsDeleted);
        Assert.Equal("hidden", comment.Status);
        Assert.NotNull(comment.DeletedAt);
        Assert.All(await db.Flags.Where(flag => flag.CommentId == reported.Comment.Id).ToListAsync(), flag => Assert.Equal("resolved", flag.Status));
    }

    [Theory]
    [InlineData("keep", "visible", "dismissed")]
    [InlineData("remove", "hidden", "resolved")]
    public async Task KeywordFilteredComment_UsesExistingQueueAndAdminDecision(string action, string status, string flagStatus)
    {
        var admin = await UserAsync(2);
        var author = await UserAsync();
        await using var db = fixture.Context();
        var post = new Post { AuthorId = author.Id, Title = "Keyword queue test", Content = "Vegan recipe",
            Status = "published", CreatedAt = DateTime.UtcNow };
        db.Posts.Add(post);
        await db.SaveChangesAsync();
        using var member = Client(author.Id, "member");
        using var reviewer = Client(admin.Id);
        var create = await member.PostAsJsonAsync($"/api/posts/{post.Id}/comments", new { content = "con đỉ mẹ mày" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var commentId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        var keywordFlag = await db.Flags.AsNoTracking().SingleAsync(flag => flag.CommentId == commentId);
        Assert.Equal("keyword", keywordFlag.SourceType);
        Assert.Null(keywordFlag.ReporterId);

        // An edit matching the filter must update the same pending review, not duplicate it.
        var edit = await member.PutAsJsonAsync($"/api/posts/{post.Id}/comments/{commentId}", new { content = "con đĩ mẹ mày" });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(1, await db.Flags.CountAsync(flag => flag.CommentId == commentId && flag.Status == "pending"));

        // Simulate a user report that existed before the comment was edited into filtered content.
        var reporter = await UserAsync();
        db.Flags.Add(new Flag { CommentId = commentId, ReporterId = reporter.Id, SourceType = "user",
            Reason = "Existing user report", Status = "pending", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var queue = await reviewer.GetFromJsonAsync<JsonElement>("/api/admin/moderation/comment-reports?PageSize=100");
        var item = Assert.Single(queue.GetProperty("items").EnumerateArray(), row => row.GetProperty("commentId").GetInt64() == commentId);
        Assert.Equal("Keyword filter", item.GetProperty("reporterName").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("reporterId").ValueKind);
        Assert.Equal(2, item.GetProperty("reportCount").GetInt32());
        var decision = await reviewer.PostAsJsonAsync($"/api/admin/moderation/comment-reports/{keywordFlag.Id}/decision",
            new { action, reason = "Reviewed filtered content" });
        Assert.Equal(HttpStatusCode.OK, decision.StatusCode);
        Assert.Equal(status, (await db.Comments.AsNoTracking().SingleAsync(comment => comment.Id == commentId)).Status);
        Assert.Equal(flagStatus, (await db.Flags.AsNoTracking().SingleAsync(flag => flag.Id == keywordFlag.Id)).Status);
        Assert.All(await db.Flags.AsNoTracking().Where(flag => flag.CommentId == commentId).ToListAsync(),
            flag => Assert.Equal(flagStatus, flag.Status));
    }

    [Fact]
    public async Task Queue_PaginatesGroupsAndRejectsMoreThan100ItemsPerPage()
    {
        var admin = await UserAsync(2);
        await ReportedCommentAsync();
        await ReportedCommentAsync();
        using var client = Client(admin.Id);
        var first = await client.GetFromJsonAsync<JsonElement>("/api/admin/moderation/comment-reports?PageIndex=1&PageSize=1");
        var second = await client.GetFromJsonAsync<JsonElement>("/api/admin/moderation/comment-reports?PageIndex=2&PageSize=1");
        Assert.Single(first.GetProperty("items").EnumerateArray());
        Assert.Single(second.GetProperty("items").EnumerateArray());
        Assert.NotEqual(first.GetProperty("items")[0].GetProperty("commentId").GetInt64(), second.GetProperty("items")[0].GetProperty("commentId").GetInt64());
        Assert.True(first.GetProperty("totalPages").GetInt32() >= 2);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/admin/moderation/comment-reports?PageSize=101")).StatusCode);
    }

    [Fact]
    public async Task Migration_QueuesOnlyOrphanedHiddenCommentsWithoutDeletingContent()
    {
        var settings = new NpgsqlConnectionStringBuilder(fixture.Connection);
        Assert.Contains(settings.Host, new[] { "localhost", "127.0.0.1", "::1" });
        var name = "member2_test_keyword_migration_" + Guid.NewGuid().ToString("N");
        await using (var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(fixture.Connection) { Database = "postgres" }.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\" TEMPLATE template0 LOCALE_PROVIDER icu ICU_LOCALE 'und'", connection);
            await command.ExecuteNonQueryAsync();
        }
        settings.Database = name;
        await using var db = new VeganHelper.DAL.Persistence.AppDbContext(
            new DbContextOptionsBuilder<VeganHelper.DAL.Persistence.AppDbContext>().UseNpgsql(settings.ConnectionString).Options);
        await db.GetService<IMigrator>().MigrateAsync("20261027091730_AddMealPlanNames");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO users (id,username,email,role_id,is_active) VALUES (900001,'keyword_migration','keyword_migration@example.invalid',1,true);
            INSERT INTO posts (id,author_id,title,status) VALUES (900001,900001,'Keyword migration test','published');
            INSERT INTO comments (id,post_id,user_id,content,status,is_deleted,deleted_at) VALUES
                (900001,900001,900001,'Hidden orphan','hidden',false,NULL),
                (900002,900001,900001,'Visible','visible',false,NULL),
                (900003,900001,900001,'Deleted','hidden',true,CURRENT_TIMESTAMP),
                (900004,900001,900001,'Already reported','hidden',false,NULL);
            INSERT INTO flags (comment_id,reporter_id,source_type,reason,status)
                VALUES (900004,900001,'user','User report','pending');
            """);
        await db.Database.MigrateAsync();
        var keyword = Assert.Single(await db.Flags.Where(flag => flag.SourceType == "keyword").ToListAsync());
        Assert.Equal(900001, keyword.CommentId);
        Assert.Null(keyword.ReporterId);
        Assert.Equal(4, await db.Comments.CountAsync());
        Assert.Equal("Hidden orphan", (await db.Comments.SingleAsync(comment => comment.Id == 900001)).Content);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
