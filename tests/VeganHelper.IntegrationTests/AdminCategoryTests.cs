using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class AdminCategoryTests(Member2ApiFixture fixture)
{
    private async Task<long> UserAsync(int role = 2)
    {
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User { Username = "category_" + suffix, Email = suffix + "@example.invalid", RoleId = role, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }
    private HttpClient Client(long id)
    {
        var client = fixture.Factory.Client();
        var token = new JwtSecurityToken("member2-test", "member2-test",
            [new Claim("sub", id.ToString()), new Claim(ClaimTypes.Role, "admin"), new Claim("token_version", "0")],
            expires: DateTime.UtcNow.AddHours(1), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Member2ApiFactory.Key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
    private async Task<Category> CategoryAsync(string kind = "recipe", string type = "post", bool active = true)
    {
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var category = new Category { Name = "Đậu %_ " + suffix, Slug = "cat-" + suffix, CategoryType = type,
            PostCategoryKind = type == "post" ? kind : null, IsActive = active };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task AdminCategories_WhenGuestOrMember_DeniesEveryMethod(string method)
    {
        var category = await CategoryAsync();
        using var guest = fixture.Factory.Client();
        using var member = Client(await UserAsync(1));
        var path = "/api/admin/categories" + (method is "PUT" or "DELETE" ? $"/{category.Id}" : "");

        using var guestRequest = new HttpRequestMessage(new(method), path) { Content = JsonContent.Create(new { name = "Test", slug = "test", postCategoryKind = "recipe" }) };
        using var memberRequest = new HttpRequestMessage(new(method), path) { Content = JsonContent.Create(new { name = "Test", slug = "test", postCategoryKind = "recipe" }) };
        var anonymous = await guest.SendAsync(guestRequest);
        var forbidden = await member.SendAsync(memberRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task CategoryCrud_WhenValid_ReturnsDtosAndCommitsAuditForEachOperation()
    {
        var admin = await UserAsync();
        using var client = Client(admin);
        var slug = "fn40-" + Guid.NewGuid().ToString("N");

        var created = await client.PostAsJsonAsync("/api/admin/categories", new { name = "  Món chay " + slug + "  ", slug = slug.ToUpperInvariant(), postCategoryKind = "recipe" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var category = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = category.GetProperty("id").GetInt32();
        Assert.Equal("Món chay " + slug, category.GetProperty("name").GetString());
        Assert.Equal(slug, category.GetProperty("slug").GetString());
        Assert.Equal($"/api/admin/categories/{id}", created.Headers.Location?.AbsolutePath);
        var updated = await client.PutAsJsonAsync($"/api/admin/categories/{id}", new { name = "Món mới " + slug, slug = slug + "-new", postCategoryKind = "food" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/admin/categories/{id}");
        Assert.Equal("Món mới " + slug, detail.GetProperty("name").GetString());
        Assert.Equal("food", detail.GetProperty("postCategoryKind").GetString());
        Assert.Equal(0, detail.GetProperty("postCount").GetInt64());
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/admin/categories/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/admin/categories/{id}")).StatusCode);
        await using var db = fixture.Context();
        var actions = await db.AdminAuditLogs.Where(a => a.AdminId == admin).OrderBy(a => a.Id).Select(a => a.Action).ToListAsync();
        Assert.Equal(new[] { "category.create", "category.update", "category.view", "category.delete" }, actions);
        Assert.False(await db.Categories.AnyAsync(c => c.Id == id));
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("pending_review")]
    [InlineData("published")]
    [InlineData("deleted")]
    public async Task DeleteCategory_WhenAnyPostStillLinked_ReturnsConflictAndPreservesData(string status)
    {
        var admin = await UserAsync();
        var category = await CategoryAsync();
        await using (var db = fixture.Context())
        {
            var post = new Post { AuthorId = admin, Title = "Linked", Content = "Test", PostType = "recipe", Status = status == "deleted" ? "published" : status,
                IsDeleted = status == "deleted", DeletedAt = status == "deleted" ? DateTime.UtcNow : null, CreatedAt = DateTime.UtcNow };
            post.PostCategories.Add(new PostCategory { CategoryId = category.Id });
            db.Posts.Add(post);
            await db.SaveChangesAsync();
        }
        using var client = Client(admin);

        var response = await client.DeleteAsync($"/api/admin/categories/{category.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var verify = fixture.Context();
        Assert.True(await verify.Categories.AnyAsync(c => c.Id == category.Id));
        Assert.True(await verify.PostCategories.AnyAsync(c => c.CategoryId == category.Id));
        Assert.False(await verify.AdminAuditLogs.AnyAsync(a => a.AdminId == admin && a.Action == "category.delete"));
    }

    [Fact]
    public async Task ListCategories_WhenSearchingAndFiltering_ReturnsPagedPostCategoriesAndLiteralUnicodeMatches()
    {
        var admin = await UserAsync();
        var first = await CategoryAsync(active: false);
        var shop = await CategoryAsync(type: "shop");
        using var client = Client(admin);
        var keyword = Uri.EscapeDataString(first.Name.ToLowerInvariant());

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/admin/categories?Keyword={keyword}&PostCategoryKind=recipe&IsActive=false&PageSize=1");
        var hidden = await client.GetAsync($"/api/admin/categories/{shop.Id}");

        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        var item = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(first.Id, item.GetProperty("id").GetInt32());
        Assert.False(item.GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        await using var db = fixture.Context();
        Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.AdminId == admin && a.Action == "category.list"));
    }

    [Theory]
    [InlineData("PageIndex=0")]
    [InlineData("PageSize=101")]
    [InlineData("PostCategoryKind=unknown")]
    public async Task ListCategories_WhenQueryInvalid_ReturnsBadRequest(string query)
    {
        using var client = Client(await UserAsync());

        var response = await client.GetAsync("/api/admin/categories?" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("", "valid", "recipe")]
    [InlineData("   ", "valid", "recipe")]
    [InlineData("Valid", "bad/slug", "recipe")]
    [InlineData("Valid", "valid", "shop")]
    [InlineData("Valid", "valid", "")]
    public async Task CreateCategory_WhenInputInvalid_ReturnsBadRequest(string name, string slug, string kind)
    {
        using var client = Client(await UserAsync());

        var response = await client.PostAsJsonAsync("/api/admin/categories", new { name, slug, postCategoryKind = kind });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WhenNameOrSlugDuplicate_ReturnsConflict()
    {
        var category = await CategoryAsync();
        using var client = Client(await UserAsync());

        var name = await client.PostAsJsonAsync("/api/admin/categories", new { name = category.Name.ToUpperInvariant(), slug = "other-" + Guid.NewGuid().ToString("N"), postCategoryKind = "recipe" });
        var slug = await client.PostAsJsonAsync("/api/admin/categories", new { name = Guid.NewGuid().ToString(), slug = category.Slug.ToUpperInvariant(), postCategoryKind = "recipe" });

        Assert.Equal(HttpStatusCode.Conflict, name.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, slug.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WhenConcurrentCaseVariantNames_OnlyOneCommits()
    {
        var admin = await UserAsync();
        using var first = Client(admin);
        using var second = Client(admin);
        var name = "ĐẬU " + Guid.NewGuid().ToString("N");

        var responses = await Task.WhenAll(first.PostAsJsonAsync("/api/admin/categories", new { name, slug = "a-" + Guid.NewGuid().ToString("N"), postCategoryKind = "recipe" }),
            second.PostAsJsonAsync("/api/admin/categories", new { name = name.ToLowerInvariant(), slug = "b-" + Guid.NewGuid().ToString("N"), postCategoryKind = "recipe" }));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        await using var db = fixture.Context();
        Assert.Equal(1, await db.AdminAuditLogs.CountAsync(a => a.AdminId == admin && a.Action == "category.create"));
    }

    [Fact]
    public async Task UpdateCategory_WhenUsed_AllowsRenameButRejectsKindChange()
    {
        var admin = await UserAsync();
        var category = await CategoryAsync();
        await using (var db = fixture.Context())
        {
            var post = new Post { AuthorId = admin, Title = "Linked", Content = "Test", PostType = "recipe", Status = "published", CreatedAt = DateTime.UtcNow };
            post.PostCategories.Add(new PostCategory { CategoryId = category.Id });
            db.Posts.Add(post);
            await db.SaveChangesAsync();
        }
        using var client = Client(admin);

        var rename = await client.PutAsJsonAsync($"/api/admin/categories/{category.Id}", new { name = category.Name + " updated", slug = category.Slug + "-new", postCategoryKind = "recipe" });
        var kind = await client.PutAsJsonAsync($"/api/admin/categories/{category.Id}", new { name = category.Name, slug = category.Slug, postCategoryKind = "topic" });

        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, kind.StatusCode);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/admin/categories/{category.Id}");
        Assert.Equal(1, detail.GetProperty("postCount").GetInt64());
        Assert.Equal("recipe", detail.GetProperty("postCategoryKind").GetString());
        Assert.Equal(category.Name + " updated", detail.GetProperty("name").GetString());
    }

    [Fact]
    public async Task UpdateCategory_WhenNameOrSlugConflict_RollsBackWithoutSuccessAudit()
    {
        var admin = await UserAsync();
        var first = await CategoryAsync();
        var second = await CategoryAsync();
        using var client = Client(admin);

        var name = await client.PutAsJsonAsync($"/api/admin/categories/{second.Id}", new { name = first.Name.ToUpperInvariant(), slug = second.Slug, postCategoryKind = "recipe" });
        var slug = await client.PutAsJsonAsync($"/api/admin/categories/{second.Id}", new { name = second.Name, slug = first.Slug, postCategoryKind = "recipe" });

        Assert.Equal(HttpStatusCode.Conflict, name.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, slug.StatusCode);
        await using var db = fixture.Context();
        Assert.Equal(second.Name, (await db.Categories.SingleAsync(c => c.Id == second.Id)).Name);
        Assert.False(await db.AdminAuditLogs.AnyAsync(a => a.AdminId == admin && a.Action == "category.update"));
    }

    [Fact]
    public async Task CategoryMutation_WhenMissingOrShop_ReturnsNotFoundAndKeepsShop()
    {
        var admin = await UserAsync();
        var shop = await CategoryAsync(type: "shop");
        using var client = Client(admin);

        foreach (var id in new[] { shop.Id, int.MaxValue })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/admin/categories/{id}", new { name = "Update", slug = "update", postCategoryKind = "recipe" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/admin/categories/{id}")).StatusCode);
        }

        await using var db = fixture.Context();
        Assert.Equal(shop.Name, (await db.Categories.SingleAsync(c => c.Id == shop.Id)).Name);
        Assert.False(await db.AdminAuditLogs.AnyAsync(a => a.AdminId == admin));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task CategoryMutation_WhenAuditFails_RollsBackChange(string method)
    {
        var admin = await UserAsync();
        var category = await CategoryAsync();
        var slug = "rollback-" + Guid.NewGuid().ToString("N");
        await using var factory = fixture.Factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IAdminAuditRepository>();
            s.AddScoped<IAdminAuditRepository, FailingAuditRepository>();
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost") });
        using var token = Client(admin);
        client.DefaultRequestHeaders.Authorization = token.DefaultRequestHeaders.Authorization;
        using var request = new HttpRequestMessage(new(method), "/api/admin/categories" + (method == "POST" ? "" : $"/{category.Id}"))
            { Content = JsonContent.Create(new { name = slug, slug, postCategoryKind = "food" }) };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var db = fixture.Context();
        var original = await db.Categories.SingleAsync(c => c.Id == category.Id);
        Assert.Equal(category.Name, original.Name);
        Assert.Equal(category.Slug, original.Slug);
        Assert.Equal("recipe", original.PostCategoryKind);
        Assert.False(await db.Categories.AnyAsync(c => c.Slug == slug));
        Assert.False(await db.AdminAuditLogs.AnyAsync(a => a.AdminId == admin));
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
    public async Task ListCategories_WhenPaging_HasStableOrderAndPublicApiKeepsExistingContract()
    {
        var admin = await UserAsync();
        var categories = new[] { await CategoryAsync(), await CategoryAsync(), await CategoryAsync(active: false) };
        var prefix = Guid.NewGuid().ToString("N");
        await using (var db = fixture.Context())
            for (var i = 0; i < categories.Length; i++)
            {
                var id = categories[i].Id;
                await db.Categories.Where(c => c.Id == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Name, prefix + " " + i));
            }
        using var client = Client(admin);
        using var guest = fixture.Factory.Client();

        var first = await client.GetFromJsonAsync<JsonElement>($"/api/admin/categories?Keyword={prefix}&PageSize=2");
        var second = await client.GetFromJsonAsync<JsonElement>($"/api/admin/categories?Keyword={prefix}&PageSize=2&PageIndex=2");
        var publicList = await guest.GetFromJsonAsync<JsonElement>("/api/categories");

        Assert.Equal(3, first.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, first.GetProperty("totalPages").GetInt32());
        Assert.Equal(new[] { categories[0].Id, categories[1].Id }, first.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetInt32()));
        Assert.Equal(categories[2].Id, Assert.Single(second.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Array, publicList.ValueKind);
        Assert.Contains(publicList.EnumerateArray(), c => c.GetProperty("id").GetInt32() == categories[0].Id);
        Assert.DoesNotContain(publicList.EnumerateArray(), c => c.GetProperty("id").GetInt32() == categories[2].Id);
    }

    [Fact]
    public async Task DeleteCategory_WhenConcurrent_DeletesOnceAndAuditsOnce()
    {
        var admin = await UserAsync();
        var category = await CategoryAsync();
        using var first = Client(admin);
        using var second = Client(admin);

        var responses = await Task.WhenAll(first.DeleteAsync($"/api/admin/categories/{category.Id}"), second.DeleteAsync($"/api/admin/categories/{category.Id}"));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NotFound);
        await using var db = fixture.Context();
        Assert.Equal(1, await db.AdminAuditLogs.CountAsync(a => a.AdminId == admin && a.Action == "category.delete"));
    }

    [Theory]
    [InlineData(101, 10)]
    [InlineData(10, 121)]
    public async Task CreateCategory_WhenTextExceedsLimit_ReturnsBadRequest(int nameLength, int slugLength)
    {
        using var client = Client(await UserAsync());

        var response = await client.PostAsJsonAsync("/api/admin/categories", new { name = new string('a', nameLength), slug = new string('b', slugLength), postCategoryKind = "recipe" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
