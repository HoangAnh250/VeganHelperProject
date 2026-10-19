using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using VeganHelper.DAL.Entities;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class AdminCategorySlugTests(Member2ApiFixture fixture)
{
    private async Task<HttpClient> AdminAsync()
    {
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User { Username = "slug_" + suffix, Email = suffix + "@example.invalid", RoleId = 2, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user); await db.SaveChangesAsync();
        var client = fixture.Factory.Client();
        var token = new JwtSecurityToken("member2-test", "member2-test",
            [new("sub", user.Id.ToString()), new(ClaimTypes.Role, "admin"), new("token_version", "0")],
            expires: DateTime.UtcNow.AddHours(1), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Member2ApiFactory.Key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    [Theory]
    [InlineData("Roasted Vegetable Soups", "roasted-vegetable-soups")]
    [InlineData("Crème & Tofu", "creme-tofu")]
    [InlineData("Đậu Hũ", "dau-hu")]
    public async Task Create_WithoutSlug_GeneratesNormalizedSlug(string name, string expected)
    {
        using var client = await AdminAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var response = await client.PostAsJsonAsync("/api/admin/categories", new { name = "  " + name + " " + suffix + "  ", postCategoryKind = "recipe" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expected + "-" + suffix, result.GetProperty("slug").GetString());
        Assert.Equal(name + " " + suffix, result.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Create_WhenDifferentNamesGenerateSameSlug_AllocatesSuffix()
    {
        using var client = await AdminAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var first = await client.PostAsJsonAsync("/api/admin/categories", new { name = "Sesame & Tofu " + suffix, postCategoryKind = "recipe" });
        var second = await client.PostAsJsonAsync("/api/admin/categories", new { name = "Sesame - Tofu " + suffix, postCategoryKind = "recipe" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var result = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("sesame-tofu-" + suffix + "-2", result.GetProperty("slug").GetString());
    }

    [Fact]
    public async Task Create_WhenNameIsDuplicate_ReturnsConflictInsteadOfCreatingCopy()
    {
        using var client = await AdminAsync();
        var name = "Seasonal Vegetables " + Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/admin/categories", new { name, postCategoryKind = "food" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/admin/categories", new { name = name.ToUpperInvariant(), postCategoryKind = "food" })).StatusCode);
    }

    [Fact]
    public async Task Update_WithoutSlug_PreservesExistingLink()
    {
        using var client = await AdminAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var response = await client.PostAsJsonAsync("/api/admin/categories", new { name = "Original " + suffix, slug = "existing-link-" + suffix, postCategoryKind = "recipe" });
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var update = await client.PutAsJsonAsync("/api/admin/categories/" + created.GetProperty("id").GetInt32(), new { name = "Renamed " + suffix, postCategoryKind = "recipe" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var result = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("existing-link-" + suffix, result.GetProperty("slug").GetString());
        Assert.Equal("Renamed " + suffix, result.GetProperty("name").GetString());
    }

    [Fact]
    public async Task ConcurrentCreation_WithSameGeneratedSlug_CreatesDistinctLinks()
    {
        using var client = await AdminAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/admin/categories", new { name = "Green & Bowls " + suffix, postCategoryKind = "recipe" }),
            client.PostAsJsonAsync("/api/admin/categories", new { name = "Green + Bowls " + suffix, postCategoryKind = "recipe" }));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var results = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<JsonElement>()));
        Assert.Equal(2, results.Select(result => result.GetProperty("slug").GetString()).Distinct().Count());
    }
}
