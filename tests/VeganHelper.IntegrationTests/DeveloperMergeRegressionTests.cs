using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using VeganHelper.BLL.Services;
using VeganHelper.BLL.DTOs.Posts;
using VeganHelper.BLL.Services.Media;
using VeganHelper.DAL.Entities;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class DeveloperMergeRegressionTests(Member2ApiFixture fixture)
{
    private async Task<long> UserAsync()
    {
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User { Username = "merge_" + suffix, Email = suffix + "@example.invalid", RoleId = 1, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private sealed record Recipe(long Id, long AuthorId, int CategoryId, int OtherCategoryId,
        long[] MediaIds, string[] MediaUrls, long[] IngredientIds, long FirstStepId);

    private async Task<Recipe> RecipeAsync()
    {
        var owner = await UserAsync();
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var categories = Enumerable.Range(1, 2).Select(i => new Category
        {
            Name = $"Merge {suffix}-{i}",
            Slug = $"merge-{suffix}-{i}",
            CategoryType = "post",
            PostCategoryKind = "recipe",
            IsActive = true
        }).ToArray();
        var ingredients = Enumerable.Range(1, 2).Select(i => new Ingredient { Name = $"Merge ingredient {suffix}-{i}", DefaultUnit = "g" }).ToArray();
        db.AddRange(categories);
        db.AddRange(ingredients);
        await db.SaveChangesAsync();
        var post = new Post { AuthorId = owner, Title = "Original recipe", Content = "Recipe", PostType = "recipe", Status = "published", CreatedAt = DateTime.UtcNow };
        post.PostCategories.Add(new PostCategory { CategoryId = categories[0].Id });
        foreach (var ingredient in ingredients)
            post.PostIngredients.Add(new PostIngredient { IngredientId = ingredient.Id, Quantity = 1, Unit = "g" });
        for (var i = 1; i <= 4; i++)
            post.Media.Add(new PostMedia { MediaUrl = $"https://media.example.invalid/{suffix}/old-{i}.jpg", MediaType = "image", ProcessingStatus = "ready", IsPrimary = i == 1, DisplayOrder = i - 1, CreatedAt = DateTime.UtcNow });
        post.PostSteps.Add(new PostStep { StepNumber = 1, Description = "Wash ingredients" });
        post.PostSteps.Add(new PostStep { StepNumber = 2, Description = "Cook ingredients" });
        db.Posts.Add(post);
        await db.SaveChangesAsync();
        var ordered = post.Media.OrderBy(m => m.DisplayOrder).ToArray();
        db.PostSummaries.Add(new PostSummary { PostId = post.Id, SourceMediaId = ordered[0].Id, Status = "pending", RequestedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return new(post.Id, owner, categories[0].Id, categories[1].Id, ordered.Select(m => m.Id).ToArray(), ordered.Select(m => m.MediaUrl).ToArray(), ingredients.Select(i => i.Id).ToArray(), post.PostSteps.Single(s => s.StepNumber == 1).Id);
    }

    private WebApplicationFactory<Program> Factory(RecordingStorage storage) => fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
    {
        services.RemoveAll<IMediaStorageService>();
        services.AddSingleton<IMediaStorageService>(storage);
        services.AddSingleton<ILoggerProvider>(new ErrorCapture(storage));
    }));

    private HttpClient Client(WebApplicationFactory<Program> factory, long owner)
    {
        using var tokenClient = fixture.Factory.Client(owner);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = tokenClient.DefaultRequestHeaders.Authorization;
        return client;
    }

    private static byte[] Jpeg() => [0xff, 0xd8, 0xff, 0xe0, 0, 0, 0, 0, 0, 0, 0, 0];
    private static IFormFile File(string name) => new FormFile(new MemoryStream(Jpeg()), 0, Jpeg().Length, "MediaFilesToAdd", name)
    { Headers = new HeaderDictionary(), ContentType = "image/jpeg" };
    private static MultipartFormDataContent Form(Recipe recipe, bool changeRelations, bool keepCover = false, bool replaceAll = false)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("Updated recipe"), "Title" },
            { new StringContent("Updated content"), "Content" },
            { new StringContent((changeRelations ? recipe.OtherCategoryId : recipe.CategoryId).ToString()), "CategoryId" }
        };
        var removals = replaceAll ? recipe.MediaIds : keepCover ? recipe.MediaIds.Skip(2) : new[] { recipe.MediaIds[0], recipe.MediaIds[2] };
        foreach (var id in removals) form.Add(new StringContent(id.ToString()), "MediaIdsToRemove");
        var ingredients = changeRelations
            ? JsonSerializer.Serialize(new object[] { new { IngredientId = recipe.IngredientIds[1], Quantity = 2, Unit = "g" }, new { Name = "New merge ingredient " + Guid.NewGuid().ToString("N"), Quantity = 3, Unit = "g" } })
            : JsonSerializer.Serialize(recipe.IngredientIds.Select(id => new { IngredientId = id, Quantity = 2, Unit = "g" }));
        form.Add(new StringContent(ingredients), "IngredientsJson");
        form.Add(new StringContent(changeRelations
            ? "[{\"StepNumber\":1,\"Description\":\"Wash carefully\"},{\"StepNumber\":3,\"Description\":\"Serve\"}]"
            : "[{\"StepNumber\":1,\"Description\":\"Wash carefully\"},{\"StepNumber\":2,\"Description\":\"Cook carefully\"}]"), "StepsJson");
        foreach (var name in new[] { "new-a.jpg", "new-b.jpg" })
        {
            var image = new ByteArrayContent(Jpeg());
            image.Headers.ContentType = new("image/jpeg");
            form.Add(image, "MediaFilesToAdd", name);
        }
        return form;
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    public async Task UpdatePost_WhenEditingMediaAndRecipe_PersistsBothMergedBehaviors(bool changeRelations, bool keepCover, bool replaceAll)
    {
        var recipe = await RecipeAsync();
        var storage = new RecordingStorage();
        using var factory = Factory(storage);
        using var client = Client(factory, recipe.AuthorId);
        using var body = Form(recipe, changeRelations, keepCover, replaceAll);

        var response = await client.PutAsync($"/api/Posts/{recipe.Id}", body);

        Assert.True(response.StatusCode == HttpStatusCode.OK, string.Join("\n", storage.Errors));
        await using var db = fixture.Context();
        var saved = await db.Posts.Include(p => p.Media).Include(p => p.PostCategories).Include(p => p.PostIngredients).Include(p => p.PostSteps).SingleAsync(p => p.Id == recipe.Id);
        var media = saved.Media.OrderBy(m => m.DisplayOrder).ToArray();
        var keptIds = replaceAll ? [] : keepCover ? recipe.MediaIds.Take(2).ToArray() : new[] { recipe.MediaIds[1], recipe.MediaIds[3] };
        Assert.Equal(keptIds.Length + 2, media.Length);
        Assert.Equal(keptIds, media.Take(keptIds.Length).Select(m => m.Id));
        Assert.Equal(storage.Uploaded, media.Skip(keptIds.Length).Select(m => m.MediaUrl));
        Assert.Equal(Enumerable.Range(0, media.Length), media.Select(m => m.DisplayOrder));
        Assert.Equal(media[0].Id, Assert.Single(media, m => m.IsPrimary).Id);
        var removedUrls = recipe.MediaIds.Select((id, index) => (id, index)).Where(x => !keptIds.Contains(x.id)).Select(x => recipe.MediaUrls[x.index]);
        Assert.Equal(removedUrls, storage.Deleted);
        Assert.Equal(keepCover, await db.PostSummaries.AnyAsync(s => s.PostId == recipe.Id));
        Assert.Equal(changeRelations ? recipe.OtherCategoryId : recipe.CategoryId, Assert.Single(saved.PostCategories).CategoryId);
        Assert.Equal(2, saved.PostIngredients.Count);
        Assert.Equal(2m, saved.PostIngredients.Single(i => i.IngredientId == recipe.IngredientIds[1]).Quantity);
        Assert.Equal(!changeRelations, saved.PostIngredients.Any(i => i.IngredientId == recipe.IngredientIds[0]));
        Assert.Equal(recipe.FirstStepId, saved.PostSteps.Single(s => s.StepNumber == 1).Id);
        Assert.Equal(changeRelations ? new[] { 1, 3 } : new[] { 1, 2 }, saved.PostSteps.Select(s => s.StepNumber).Order());
        Assert.Equal("pending_review", saved.Status);
    }

    [Fact]
    public async Task UpdatePost_WhenAnotherUserCallsApi_RejectsBeforeMediaChanges()
    {
        var recipe = await RecipeAsync();
        var storage = new RecordingStorage();
        using var factory = Factory(storage);
        using var client = Client(factory, await UserAsync());
        using var body = Form(recipe, true);

        var response = await client.PutAsync($"/api/Posts/{recipe.Id}", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(storage.Uploaded);
        Assert.Empty(storage.Deleted);
        await using var db = fixture.Context();
        Assert.Equal(4, await db.PostMedia.CountAsync(m => m.PostId == recipe.Id));
        Assert.True(await db.PostSummaries.AnyAsync(s => s.PostId == recipe.Id));
    }

    [Fact]
    public async Task UpdatePost_WhenDatabaseRejectsCategory_RollsBackAndCleansOnlyNewUploads()
    {
        var recipe = await RecipeAsync();
        var storage = new RecordingStorage();
        using var factory = Factory(storage);
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPostService>();
        var request = new UpdatePostRequest
        {
            Title = "Updated",
            Content = "Recipe",
            CategoryId = int.MaxValue,
            MediaIdsToRemove = [recipe.MediaIds[0], recipe.MediaIds[2]],
            MediaFilesToAdd = [File("new-a.jpg"), File("new-b.jpg")]
        };

        await Assert.ThrowsAsync<DbUpdateException>(() => service.UpdatePostAsync(recipe.Id, request, recipe.AuthorId));

        Assert.Equal(2, storage.Uploaded.Count);
        Assert.Equal(storage.Uploaded, storage.Deleted);
        await using var db = fixture.Context();
        Assert.Equal(recipe.MediaIds.Order(), (await db.PostMedia.Where(m => m.PostId == recipe.Id).Select(m => m.Id).ToListAsync()).Order());
        Assert.True(await db.PostSummaries.AnyAsync(s => s.PostId == recipe.Id));
        Assert.Equal("Original recipe", (await db.Posts.SingleAsync(p => p.Id == recipe.Id)).Title);
        Assert.Equal(recipe.CategoryId, (await db.PostCategories.SingleAsync(c => c.PostId == recipe.Id)).CategoryId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Login_WhenUsingMergedIdentifierContract_AcceptsUsernameOrEmail(bool email)
    {
        const string password = "Local-merge-test-391!";
        var owner = await UserAsync();
        await using var db = fixture.Context();
        var user = await db.Users.SingleAsync(u => u.Id == owner);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4);
        user.EmailVerifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        using var client = fixture.Factory.Client();

        var response = await client.PostAsJsonAsync("/api/Auth/login", new { identifier = email ? user.Email : user.Username, password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("accessToken").GetString()));
    }

    private sealed class RecordingStorage : IMediaStorageService
    {
        public List<string> Errors { get; } = [];
        public List<string> Uploaded { get; } = [];
        public List<string> Deleted { get; } = [];
        public Task<string> UploadFileAsync(IFormFile file, string folder)
        {
            var url = $"https://media.example.invalid/{Guid.NewGuid():N}/{file.FileName}";
            Uploaded.Add(url);
            return Task.FromResult(url);
        }
        public Task DeleteFileAsync(string fileUrl) { Deleted.Add(fileUrl); return Task.CompletedTask; }
    }

    private sealed class ErrorCapture(RecordingStorage storage) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(storage);
        public void Dispose() { }
        private sealed class CaptureLogger(RecordingStorage storage) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => level >= LogLevel.Error;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            { if (level >= LogLevel.Error && exception is not null) storage.Errors.Add(exception.ToString()); }
        }
    }
}
