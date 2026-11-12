using System.Net;
using VeganHelper.DAL.Entities;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class PostSummaryAccessTests(Member2ApiFixture fixture)
{
    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task GuestCannotReadOrGenerateSummary(string method)
    {
        using var client = fixture.Factory.Client();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/posts/244/summary");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AuthenticatedMemberCanReachSummaryRead()
    {
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User { Username = "summary_" + suffix, Email = suffix + "@example.invalid", IsActive = true, RoleId = 1, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user); await db.SaveChangesAsync();
        using var client = fixture.Factory.Client(user.Id);
        using var response = await client.GetAsync("/api/posts/9223372036854775807/summary");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
