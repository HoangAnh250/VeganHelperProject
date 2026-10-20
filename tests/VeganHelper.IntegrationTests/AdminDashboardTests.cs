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
public sealed class AdminDashboardTests(Member2ApiFixture fixture)
{
    private async Task<User> UserAsync(int role, DateTime? created = null)
    {
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User { Username = "dash_" + suffix, Email = suffix + "@example.invalid", RoleId = role,
            PasswordHash = "not-used", IsActive = true, EmailVerifiedAt = DateTime.UtcNow, CreatedAt = created ?? DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
    private HttpClient Client(User user)
    {
        var client = fixture.Factory.Client();
        var token = new JwtSecurityToken("member2-test", "member2-test",
            [new("sub", user.Id.ToString()), new(ClaimTypes.Role, user.RoleId == 2 ? "admin" : "member"), new("token_version", "0")],
            expires: DateTime.UtcNow.AddHours(1), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Member2ApiFactory.Key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    [Theory]
    [InlineData("overview")]
    [InlineData("post-activity")]
    [InlineData("top-categories")]
    public async Task Dashboard_RequiresAdmin(string endpoint)
    {
        using var guest = fixture.Factory.Client();
        using var member = Client(await UserAsync(1));
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/admin/dashboard/" + endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/admin/dashboard/" + endpoint)).StatusCode);
    }

    [Theory]
    [InlineData("overview?from=2001-02-02&to=2001-02-01")]
    [InlineData("overview?from=2001-01-01&to=2002-01-02")]
    [InlineData("overview?to=9999-12-31")]
    [InlineData("overview?to=0001-01-01")]
    [InlineData("overview?from=invalid")]
    [InlineData("post-activity?interval=invalid")]
    [InlineData("top-categories?limit=0")]
    [InlineData("top-categories?limit=11")]
    public async Task Dashboard_RejectsInvalidFilters(string path)
    {
        using var client = Client(await UserAsync(2));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/admin/dashboard/" + path)).StatusCode);
    }

    [Fact]
    public async Task Dashboard_ReturnsAccurateCountsBucketsCategoriesAndAudit()
    {
        static DateTime Day(int day) => new(2001, 2, day, 0, 0, 0, DateTimeKind.Utc);
        var admin = await UserAsync(2);
        using var client = Client(admin);
        const string range = "from=2001-02-01&to=2001-02-03";
        var baseline = await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard/overview?from=2001-01-31&to=2001-02-03");
        var author = await UserAsync(1, Day(1));
        await UserAsync(1, new DateTime(2001, 1, 31, 0, 0, 0, DateTimeKind.Utc));
        await UserAsync(2, Day(1)); // Administrators are excluded from registered-member metrics.
        var reporter = await UserAsync(1);
        int categoryId;
        await using (var db = fixture.Context())
        {
            var posts = new[] {
                new Post { AuthorId = author.Id, Title = "Visible", PostType = "recipe", Status = "published", CreatedAt = Day(1) },
                new Post { AuthorId = author.Id, Title = "Held", PostType = "recipe", Status = "pending_review", ModerationHold = true, CreatedAt = Day(2) },
                new Post { AuthorId = author.Id, Title = "Deleted", PostType = "recipe", Status = "published", IsDeleted = true, DeletedAt = Day(1), CreatedAt = Day(1) },
                new Post { AuthorId = author.Id, Title = "Outside range", PostType = "recipe", Status = "published", CreatedAt = Day(4) },
                new Post { AuthorId = author.Id, Title = "End of last day", PostType = "recipe", Status = "published", CreatedAt = Day(3).AddDays(1).AddTicks(-10) }
            };
            db.Posts.AddRange(posts);
            var cat = new Category { Name = "Dashboard test", Slug = Guid.NewGuid().ToString("N"), CategoryType = "post", PostCategoryKind = "recipe", IsActive = true };
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
            categoryId = cat.Id;
            db.PostCategories.AddRange(posts.Select(p => new PostCategory { PostId = p.Id, CategoryId = cat.Id }));
            var comment = new Comment { PostId = posts[0].Id, UserId = author.Id, Content = "Reported", Status = "visible", CreatedAt = Day(1) };
            db.Comments.Add(comment);
            await db.SaveChangesAsync();
            db.Flags.AddRange(
                new Flag { PostId = posts[1].Id, SourceType = "ai", AiModelName = "test-gemini", PostRevision = 1, Status = "pending", Reason = "AI flag", CreatedAt = Day(2) },
                new Flag { PostId = posts[1].Id, SourceType = "user", ReporterId = reporter.Id, PostRevision = 1, Status = "pending", Reason = "Report", CreatedAt = Day(2) },
                new Flag { PostId = posts[1].Id, SourceType = "user", ReporterId = author.Id, PostRevision = 1, Status = "pending", Reason = "Second report", CreatedAt = Day(2) },
                new Flag { CommentId = comment.Id, SourceType = "user", ReporterId = reporter.Id, Status = "pending", Reason = "Comment report", CreatedAt = Day(2) });
            await db.SaveChangesAsync();
        }
        var overview = await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard/overview?" + range);
        Assert.Equal(1, overview.GetProperty("members").GetProperty("newInPeriod").GetInt64());
        Assert.Equal(1, overview.GetProperty("members").GetProperty("newInPreviousPeriod").GetInt64());
        Assert.Equal(0, overview.GetProperty("members").GetProperty("growthPercentage").GetDecimal());
        Assert.Equal(3, overview.GetProperty("posts").GetProperty("createdInPeriod").GetInt64());
        foreach (var name in new[] { "aiFlaggedPosts", "reportedPosts", "reportedComments", "totalPending" })
            Assert.Equal(baseline.GetProperty("moderation").GetProperty(name).GetInt64() + (name == "totalPending" ? 2 : 1),
                overview.GetProperty("moderation").GetProperty(name).GetInt64());

        var activity = await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard/post-activity?" + range);
        Assert.Equal(3, activity.GetProperty("items").GetArrayLength());
        Assert.All(activity.GetProperty("items").EnumerateArray(), item => Assert.Equal(1, item.GetProperty("postCount").GetInt64()));
        var weekly = await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard/post-activity?" + range + "&interval=week");
        Assert.Equal("2001-01-29", weekly.GetProperty("items")[0].GetProperty("period").GetString());
        Assert.Equal(3, weekly.GetProperty("items")[0].GetProperty("postCount").GetInt64());
        var monthly = await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard/post-activity?" + range + "&interval=month");
        Assert.Equal("2001-02-01", monthly.GetProperty("items")[0].GetProperty("period").GetString());
        var empty = await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard/post-activity?from=2000-01-01&to=2000-01-02");
        Assert.Equal(2, empty.GetProperty("items").GetArrayLength());
        Assert.All(empty.GetProperty("items").EnumerateArray(), item => Assert.Equal(0, item.GetProperty("postCount").GetInt64()));
        var categories = await client.GetFromJsonAsync<JsonElement>("/api/admin/dashboard/top-categories?" + range);
        Assert.Equal(2, categories.GetProperty("totalCategorizedPosts").GetInt64());
        var category = Assert.Single(categories.GetProperty("items").EnumerateArray());
        Assert.Equal(categoryId, category.GetProperty("categoryId").GetInt32());
        Assert.Equal(2, category.GetProperty("postCount").GetInt64());
        Assert.Equal(100, category.GetProperty("percentage").GetDecimal());
        await using var verify = fixture.Context();
        Assert.True(await verify.AdminAuditLogs.AnyAsync(a => a.AdminId == admin.Id && a.Action == "dashboard.overview"));
    }
}
