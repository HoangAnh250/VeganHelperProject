using FluentValidation;
using FluentValidation.Results;
using Moq;
using VeganHelper.BLL.DTOs.Posts;
using VeganHelper.BLL.Services;
using VeganHelper.BLL.Services.Media;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.UnitTests;

public sealed class Sprint2ContentInteractionTests
{
    [Fact]
    public async Task SearchPostsAsync_RejectsKeywordShorterThanTwoCharacters()
    {
        var repository = new Mock<IPostRepository>();
        var service = CreatePostService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchPostsAsync(
            new SearchPostsRequest { Keyword = "a" }));

        repository.Verify(
            x => x.SearchPostsAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchPostsAsync_TrimsKeywordAndMapsPagedResults()
    {
        var repository = new Mock<IPostRepository>();
        var service = CreatePostService(repository);
        var projections = new List<PostFeedProjection>
        {
            new()
            {
                Id = 10,
                Title = "Tofu Bowl",
                PostType = "article",
                AuthorName = "Vegan User"
            }
        };

        repository.Setup(x => x.SearchPostsAsync("tofu", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((1L, projections));

        var result = await service.SearchPostsAsync(new SearchPostsRequest
        {
            Keyword = "  tofu  ",
            PageIndex = 0,
            PageSize = 10
        });

        Assert.Single(result.Items);
        Assert.Equal(10, result.Items.First().Id);
        Assert.Equal(1L, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
        repository.Verify(x => x.SearchPostsAsync("tofu", 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleLikeAsync_WhenLikeDoesNotExist_AddsLikeAndReturnsLiked()
    {
        var repository = new Mock<IPostInteractionRepository>();
        repository.Setup(x => x.IsPublishedPostAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.FindLikeAsync(1, 10, It.IsAny<CancellationToken>())).ReturnsAsync((PostLike?)null);
        repository.Setup(x => x.CountLikesAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(4);

        var service = new PostInteractionService(repository.Object);
        var result = await service.ToggleLikeAsync(1, 10);

        Assert.True(result.IsLiked);
        Assert.Equal(4, result.LikeCount);
        repository.Verify(x => x.AddLikeAsync(
            It.Is<PostLike>(like => like.UserId == 1 && like.PostId == 10),
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleSaveAsync_WhenSaveExists_RemovesSaveAndReturnsUnsaved()
    {
        var repository = new Mock<IPostInteractionRepository>();
        var existing = new SavedPost { UserId = 1, PostId = 10 };
        repository.Setup(x => x.IsPublishedPostAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.FindSavedPostAsync(1, 10, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var service = new PostInteractionService(repository.Object);
        var result = await service.ToggleSaveAsync(1, 10);

        Assert.False(result.IsSaved);
        repository.Verify(x => x.RemoveSavedPost(existing), Times.Once);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static PostService CreatePostService(Mock<IPostRepository> repository) => new(
        repository.Object,
        new Mock<IValidator<CreatePostRequest>>().Object,
        new Mock<IValidator<GetMyPostsRequest>>().Object,
        new Mock<IValidator<UpdatePostRequest>>().Object,
        new Mock<IMediaStorageService>().Object);
}
