using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VeganHelper.DAL.Entities;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class SearchBrowseApiTests(Member2ApiFixture fixture)
{
    private async Task<long> Member()
    {
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User { Username = "search_" + suffix, Email = suffix + "@example.invalid",
            IsActive = true, RoleId = 1, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user); await db.SaveChangesAsync(); return user.Id;
    }

    [Theory]
    [InlineData("posts", "")]
    [InlineData("posts", "%20%20")]
    [InlineData("posts", "%09")]
    [InlineData("users", "")]
    [InlineData("users", "%20%20")]
    [InlineData("users", "%09")]
    public async Task ExplicitEmptyKeywordIs400ThroughHttpBinding(string resource, string keyword)
    {
        using var client = fixture.Factory.Client(await Member());
        using var response = await client.GetAsync($"/api/{resource}/search?keyword={keyword}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("posts")]
    [InlineData("users")]
    public async Task MissingKeywordBrowsesDefaultsWithPagination(string resource)
    {
        using var client = fixture.Factory.Client(await Member());
        using var response = await client.GetAsync($"/api/{resource}/search?pageSize=1&pageIndex=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, page.GetProperty("pageSize").GetInt32());
        Assert.True(page.GetProperty("items").GetArrayLength() <= 1);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("invalid")]
    public async Task InvalidCategoryIsRejected(string category)
    {
        using var client = fixture.Factory.Client();
        using var response = await client.GetAsync($"/api/posts/search?keyword=tofu&categoryId={category}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("expert", 400)]
    [InlineData("easy%2Chard", 400)]
    [InlineData("EASY", 200)]
    [InlineData("medium", 200)]
    [InlineData("hard", 200)]
    public async Task SearchDifficultyIsValidatedAtHttpBoundary(string difficulty, int status)
    {
        using var client = fixture.Factory.Client();
        using var response = await client.GetAsync($"/api/posts/search?difficultyLevel={difficulty}");
        Assert.Equal(status, (int)response.StatusCode);
    }
}
