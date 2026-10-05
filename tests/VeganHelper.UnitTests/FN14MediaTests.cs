using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using VeganHelper.BLL.DTOs.Posts;
using VeganHelper.BLL.Services;
using VeganHelper.BLL.Services.Media;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.UnitTests;

public class FN14MediaTests
{
    private readonly Mock<IPostRepository> repository = new();
    private readonly Mock<IMediaStorageService> storage = new();
    private readonly Post post = new() { Id = 10, AuthorId = 1, Title = "Original", PostType = "recipe" };
    private readonly PostService service;

    public FN14MediaTests()
    {
        for (var i = 1; i <= 4; i++)
            post.Media.Add(new PostMedia { Id = i, MediaUrl = $"old-{i}", MediaType = "image", DisplayOrder = i - 1, IsPrimary = i == 1 });
        repository.Setup(r => r.GetPostForUpdateAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(post);
        storage.Setup(s => s.UploadFileAsync(It.IsAny<IFormFile>(), "posts"))
            .ReturnsAsync((IFormFile file, string _) => $"new-{file.FileName}");
        service = new PostService(repository.Object, Mock.Of<IValidator<CreatePostRequest>>(),
            Mock.Of<IValidator<GetMyPostsRequest>>(), new UpdatePostRequestValidator(), storage.Object);
    }

    private static UpdatePostRequest Request(List<long>? remove = null, List<IFormFile>? add = null) =>
        new() { Title = "Updated", Content = "Recipe", CategoryId = 1, MediaIdsToRemove = remove, MediaFilesToAdd = add };

    private static IFormFile Image(string name = "a.jpg", bool valid = true)
    {
        byte[] bytes = valid ? [0xff, 0xd8, 0xff, 0xe0, 0, 0, 0, 0, 0, 0, 0, 0] : new byte[12];
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "MediaFilesToAdd", name)
        { Headers = new HeaderDictionary(), ContentType = "image/jpeg" };
    }

    private void AssertCoverAndOrder()
    {
        Assert.Single(post.Media, m => m.IsPrimary);
        Assert.Equal(0, post.Media.Single(m => m.IsPrimary).DisplayOrder);
        Assert.Equal(Enumerable.Range(0, post.Media.Count), post.Media.Select(m => m.DisplayOrder).Order());
    }

    [Fact]
    public async Task MultipleRemovalsAndAdditions_PreserveSurvivorsAndPromoteCover()
    {
        await service.UpdatePostAsync(10, Request([1, 3], [Image(), Image("b.jpg")]), 1);
        Assert.Equal(new[] { "old-2", "old-4", "new-a.jpg", "new-b.jpg" }, post.Media.OrderBy(m => m.DisplayOrder).Select(m => m.MediaUrl));
        AssertCoverAndOrder();
        storage.Verify(s => s.DeleteFileAsync("old-1"), Times.Once);
        storage.Verify(s => s.DeleteFileAsync("old-3"), Times.Once);
        repository.Verify(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReplaceAllMedia_MakesFirstNewImageCover()
    {
        await service.UpdatePostAsync(10, Request([1, 2, 3, 4], [Image(), Image("b.jpg")]), 1);
        Assert.Equal(2, post.Media.Count);
        Assert.Equal("new-a.jpg", post.Media.Single(m => m.IsPrimary).MediaUrl);
        AssertCoverAndOrder();
    }

    [Fact]
    public async Task TextOnlyEdit_PreservesAllImages()
    {
        await service.UpdatePostAsync(10, Request(), 1);
        Assert.Equal(new[] { "old-1", "old-2", "old-3", "old-4" }, post.Media.Select(m => m.MediaUrl));
        AssertCoverAndOrder();
        storage.Verify(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()), Times.Never);
        storage.Verify(s => s.DeleteFileAsync(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public async Task InvalidRemovalId_RejectsWithoutSideEffects(long id)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdatePostAsync(10, Request([id], [Image()]), 1));
        Assert.Equal("Original", post.Title);
        storage.Verify(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()), Times.Never);
        repository.Verify(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveAllWithoutReplacement_RejectsAndKeepsMedia()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdatePostAsync(10, Request([1, 2, 3, 4]), 1));
        Assert.Equal(4, post.Media.Count);
        storage.Verify(s => s.DeleteFileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DuplicateRemovalIds_AreRemovedOnce()
    {
        await service.UpdatePostAsync(10, Request([1, 1]), 1);
        Assert.Equal(3, post.Media.Count);
        storage.Verify(s => s.DeleteFileAsync("old-1"), Times.Once);
        AssertCoverAndOrder();
    }

    [Fact]
    public async Task MoreThanTenMedia_RejectsBeforeUploading()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdatePostAsync(10, Request(add: Enumerable.Range(0, 7).Select(i => Image($"{i}.jpg")).ToList()), 1));
        storage.Verify(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InvalidSecondFile_ValidatesEntireBatchBeforeUploading()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdatePostAsync(10, Request([1], [Image(), Image("b.jpg", false)]), 1));
        storage.Verify(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()), Times.Never);
        Assert.Equal(4, post.Media.Count);
    }

    [Fact]
    public async Task OversizedImage_RejectsBeforeUploading()
    {
        var file = new Mock<IFormFile>();
        file.SetupGet(f => f.Length).Returns(5L * 1024 * 1024 + 1);
        file.SetupGet(f => f.FileName).Returns("large.jpg");
        file.SetupGet(f => f.ContentType).Returns("image/jpeg");
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdatePostAsync(10, Request(add: [file.Object]), 1));
        storage.Verify(s => s.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PartialUploadFailure_DeletesUploadedFileAndPreservesOldFiles()
    {
        storage.Setup(s => s.UploadFileAsync(It.Is<IFormFile>(f => f.FileName == "b.jpg"), "posts"))
            .ThrowsAsync(new IOException("Upload failed"));
        await Assert.ThrowsAsync<IOException>(() => service.UpdatePostAsync(10, Request([1, 2], [Image(), Image("b.jpg")]), 1));
        storage.Verify(s => s.DeleteFileAsync("new-a.jpg"), Times.Once);
        storage.Verify(s => s.DeleteFileAsync(It.Is<string>(url => url.StartsWith("old-"))), Times.Never);
        repository.Verify(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(4, post.Media.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DatabaseFailure_RollsBackAndCleansNewFilesEvenIfRollbackFails(bool rollbackFails)
    {
        var failure = new InvalidOperationException("Save failed");
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        if (rollbackFails) repository.Setup(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("Rollback failed"));
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdatePostAsync(10, Request([1], [Image(), Image("b.jpg")]), 1));
        Assert.Same(failure, actual);
        repository.Verify(r => r.RollbackTransactionAsync(CancellationToken.None), Times.Once);
        storage.Verify(s => s.DeleteFileAsync("new-a.jpg"), Times.Once);
        storage.Verify(s => s.DeleteFileAsync("new-b.jpg"), Times.Once);
        storage.Verify(s => s.DeleteFileAsync("old-1"), Times.Never);
    }

    [Fact]
    public async Task OldFileCleanupFailure_AfterCommitDoesNotRollbackOrDeleteNewFiles()
    {
        storage.Setup(s => s.DeleteFileAsync("old-1")).ThrowsAsync(new IOException("Storage unavailable"));
        await service.UpdatePostAsync(10, Request([1, 2], [Image()]), 1);
        storage.Verify(s => s.DeleteFileAsync("old-2"), Times.Once);
        storage.Verify(s => s.DeleteFileAsync("new-a.jpg"), Times.Never);
        repository.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains(post.Media, m => m.MediaUrl == "new-a.jpg");
    }

    [Fact]
    public async Task RetainedSharedUrl_IsNotDeletedFromStorage()
    {
        post.Media.Single(m => m.Id == 2).MediaUrl = "old-1";
        await service.UpdatePostAsync(10, Request([1]), 1);
        storage.Verify(s => s.DeleteFileAsync("old-1"), Times.Never);
    }

    [Fact]
    public async Task DetailResponse_ExposesPersistedMediaIdsAndOrder()
    {
        repository.Setup(r => r.GetPostDetailAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync((post, "Author"));
        var detail = await service.GetPostDetailAsync(10);
        Assert.Equal(new long[] { 1, 2, 3, 4 }, detail.Media.Select(m => m.Id));
        Assert.Equal(new[] { 0, 1, 2, 3 }, detail.Media.Select(m => m.DisplayOrder));
    }

    [Fact]
    public async Task EfTracking_WhenKeepingRecipeJoins_RetainsKeysAndUpdatesMedia()
    {
        // PostgreSQL model only: no database connection or cloud writes.
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=unused;Username=postgres").Options);
        foreach (var media in post.Media) media.PostId = post.Id;
        post.PostCategories.Add(new PostCategory { PostId = post.Id, CategoryId = 1 });
        post.PostIngredients.Add(new PostIngredient { PostId = post.Id, IngredientId = 1, Unit = "g" });
        context.Attach(post);
        var oldCategory = post.PostCategories.Single();
        var oldIngredient = post.PostIngredients.Single();
        repository.Setup(r => r.FindIngredientByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Ingredient { Id = 1, Name = "Ingredient", DefaultUnit = "g" });
        repository.Setup(r => r.StagePostUpdateRemovalsAsync(post, It.IsAny<IReadOnlyCollection<PostMedia>>(),
            It.IsAny<IReadOnlyCollection<PostCategory>>(), It.IsAny<IReadOnlyCollection<PostIngredient>>(),
            It.IsAny<IReadOnlyCollection<PostStep>>(), It.IsAny<CancellationToken>()))
            .Callback<Post, IReadOnlyCollection<PostMedia>, IReadOnlyCollection<PostCategory>, IReadOnlyCollection<PostIngredient>, IReadOnlyCollection<PostStep>, CancellationToken>((_, media, categories, ingredients, steps, _) =>
            {
                context.PostMedia.RemoveRange(media);
                context.PostCategories.RemoveRange(categories);
                context.PostIngredients.RemoveRange(ingredients);
                context.PostSteps.RemoveRange(steps);
            }).Returns(Task.CompletedTask);
        repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => context.ChangeTracker.DetectChanges()).Returns(Task.CompletedTask);
        var request = Request([1, 3], [Image(), Image("b.jpg")]);
        request.IngredientsJson = "[{\"IngredientId\":1,\"Quantity\":2,\"Unit\":\"g\"}]";
        await service.UpdatePostAsync(10, request, 1);
        Assert.Equal(2, context.ChangeTracker.Entries<PostMedia>().Count(e => e.State == EntityState.Deleted));
        Assert.Equal(2, context.ChangeTracker.Entries<PostMedia>().Count(e => e.State == EntityState.Added));
        Assert.Same(oldCategory, post.PostCategories.Single());
        Assert.Same(oldIngredient, post.PostIngredients.Single());
        Assert.Equal(EntityState.Unchanged, context.Entry(oldCategory).State);
        Assert.Equal(EntityState.Modified, context.Entry(oldIngredient).State);
        Assert.Equal(2m, oldIngredient.Quantity);
    }
}
