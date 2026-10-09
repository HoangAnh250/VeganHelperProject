using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Text.Json;
using VeganHelper.BLL.DTOs.Posts;
using VeganHelper.BLL.Services;
using VeganHelper.BLL.Services.Media;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.IntegrationTests;

public partial class PostRepositoryTests
{
    [Fact]
    public async Task DeveloperUserSearch_WorksWithPostgreSqlCaseInsensitiveUsername()
    {
        var user = await AddUser();
        var result = await new UserRepository(_context).SearchUsersAsync(user.Username.ToUpperInvariant(), 1, 10, default);
        Assert.Equal(user.Id, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task DeveloperPostSearch_MatchesUnicodeRegardlessOfCase()
    {
        var user = await AddUser();
        var post = NewPost(user.Id);
        post.Title = "Đậu hũ " + Guid.NewGuid().ToString("N");
        post.PostType = "article";
        post.Status = "published";
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();
        var result = await _postRepository.SearchPostsAsync(post.Title.ToUpperInvariant(), 1, 10);
        Assert.Equal(post.Id, Assert.Single(result.Items).Id);
    }

    // Real repository queries and writes, with the fixture owning the outer rollback transaction.
    private PostService RecipeService()
    {
        var repository = new Mock<IPostRepository>();
        repository.Setup(r => r.GetOrCreateIngredientAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string name, string unit, CancellationToken ct) => _postRepository.GetOrCreateIngredientAsync(name, unit, ct));
        repository.Setup(r => r.FindIngredientByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns((long id, CancellationToken ct) => _postRepository.FindIngredientByIdAsync(id, ct));
        repository.Setup(r => r.CreatePostAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()))
            .Returns((Post post, CancellationToken ct) => _postRepository.CreatePostAsync(post, ct));
        repository.Setup(r => r.GetPostForUpdateAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns((long id, CancellationToken ct) => _postRepository.GetPostForUpdateAsync(id, ct));
        repository.Setup(r => r.StagePostUpdateRemovalsAsync(It.IsAny<Post>(), It.IsAny<IReadOnlyCollection<PostMedia>>(),
            It.IsAny<IReadOnlyCollection<PostCategory>>(), It.IsAny<IReadOnlyCollection<PostIngredient>>(),
            It.IsAny<IReadOnlyCollection<PostStep>>(), It.IsAny<CancellationToken>()))
            .Returns((Post post, IReadOnlyCollection<PostMedia> media, IReadOnlyCollection<PostCategory> categories,
                IReadOnlyCollection<PostIngredient> ingredients, IReadOnlyCollection<PostStep> steps, CancellationToken ct) =>
                _postRepository.StagePostUpdateRemovalsAsync(post, media, categories, ingredients, steps, ct));
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => _postRepository.SaveChangesAsync(ct));
        repository.Setup(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => _postRepository.SaveChangesAsync(ct));
        var media = new Mock<IMediaStorageService>();
        media.Setup(m => m.UploadFileAsync(It.IsAny<IFormFile>(), "posts")).ReturnsAsync("https://example.invalid/recipe-probe.jpg");
        return new PostService(
            repository.Object,
            new CreatePostRequestValidator(),
            new GetMyPostsRequestValidator(),
            new UpdatePostRequestValidator(),
            media.Object,
            new Mock<IPostInteractionRepository>().Object);
    }

    [Fact]
    public async Task RecipeForm_CreatesNamedIngredientsAndSteps_AndUpdatesWithoutDuplicateKeys()
    {
        await DataSeeder.SeedDataAsync(_context);
        var author = await AddUser();
        var otherAuthor = await AddUser();
        var category = await _context.Categories.FirstAsync(c => c.CategoryType == "post" && c.PostCategoryKind == "recipe");
        var ingredientName = "Đậu hũ " + Guid.NewGuid().ToString("N");
        var service = RecipeService();
        using var image = new MemoryStream([1, 2, 3]);
        var request = new CreatePostRequest
        {
            Title = "Recipe form regression", PostType = "recipe", CategoryId = category.Id, Content = "Nấu món chay",
            IngredientsJson = JsonSerializer.Serialize(new[] { new { Name = ingredientName } }),
            StepsJson = "[{\"StepNumber\":1,\"Description\":\"Rửa nguyên liệu\"},{\"StepNumber\":2,\"Description\":\"Nấu chín\"}]",
            MediaFiles = new FormFileCollection { new FormFile(image, 0, image.Length, "MediaFiles", "probe.jpg") { Headers = new HeaderDictionary(), ContentType = "image/jpeg" } }
        };
        var id = await service.CreatePostAsync(request, author.Id);
        _context.ChangeTracker.Clear();
        var saved = (await _postRepository.GetPostDetailAsync(id)).Post!;
        Assert.Equal(ingredientName, Assert.Single(saved.PostIngredients).Ingredient!.Name);
        Assert.True(saved.PostIngredients.Single().IngredientId > 0);
        Assert.Equal(2, saved.PostSteps.Count);
        Assert.Equal(1, saved.ContentRevision);
        Assert.Equal(1, (await _context.Set<PostModerationScan>().SingleAsync(s => s.PostId == id)).Revision);
        var stepId = saved.PostSteps.Single(s => s.StepNumber == 1).Id;

        var update = new UpdatePostRequest
        {
            Title = request.Title, Content = request.Content, CategoryId = category.Id,
            IngredientsJson = JsonSerializer.Serialize(new[] { new { Name = ingredientName.ToUpperInvariant(), Quantity = 2, Unit = "g" } }),
            StepsJson = "[{\"StepNumber\":1,\"Description\":\"Rửa kỹ nguyên liệu\"}]"
        };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdatePostAsync(id, update, otherAuthor.Id));
        await service.UpdatePostAsync(id, update, author.Id);
        _context.ChangeTracker.Clear();
        saved = (await _postRepository.GetPostDetailAsync(id)).Post!;
        Assert.Equal(2m, Assert.Single(saved.PostIngredients).Quantity);
        Assert.Equal("g", saved.PostIngredients.Single().Unit);
        Assert.Equal(stepId, Assert.Single(saved.PostSteps).Id);
        Assert.Equal("Rửa kỹ nguyên liệu", saved.PostSteps.Single().Description);
        Assert.Equal(1, await _context.Ingredients.CountAsync(i => i.Name == ingredientName));
        Assert.Equal(2, saved.ContentRevision);
        Assert.Equal(new[] { 1, 2 }, await _context.Set<PostModerationScan>().Where(s => s.PostId == id).OrderBy(s => s.Revision).Select(s => s.Revision).ToArrayAsync());

        _context.Posts.Add(NewPost(otherAuthor.Id));
        await _context.SaveChangesAsync();
        Assert.Equal(id, Assert.Single((await _postRepository.GetMyPostsAsync(author.Id, null, 1, 50)).Posts).Id);
    }
}
