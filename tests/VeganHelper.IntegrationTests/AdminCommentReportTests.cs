using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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
        using var memberClient = Client(member.Id, "member");
        using var adminClient = Client(admin.Id);

        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/admin/moderation/comment-reports")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.GetAsync("/api/admin/moderation/comment-reports")).StatusCode);
        var response = await adminClient.GetFromJsonAsync<JsonElement>("/api/admin/moderation/comment-reports?Status=pending");

        var item = Assert.Single(response.GetProperty("items").EnumerateArray(),
            value => value.GetProperty("commentId").GetInt64() == reported.Comment.Id);
        Assert.Equal(2, item.GetProperty("reportCount").GetInt32());
        Assert.Equal("Reported comment content", item.GetProperty("commentContent").GetString());
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
}
