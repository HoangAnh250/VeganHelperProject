using FluentValidation;
using FluentValidation.Results;
using Moq;
using VeganHelper.BLL.DTOs.Posts;
using VeganHelper.BLL.Services;
using VeganHelper.BLL.Services.Media;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;
using VeganHelper.DAL.Storage;

namespace VeganHelper.UnitTests;

public sealed class Sprint2ContentInteractionTests
{
    [Fact]
    public async Task SearchUsersAsync_TrimsKeywordAndMapsPagedResults()
    {
        var repository = new Mock<IUserRepository>();
        IReadOnlyList<UserSearchProjection> projections = new List<UserSearchProjection>
        {
            new()
            {
                Id = 7,
                Username = "tofu_lover",
                DisplayName = "Tofu Lover",
                AvatarUrl = "avatar.jpg"
            }
        };
        repository.Setup(x => x.SearchUsersAsync("tofu", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((1L, projections));

        var service = new UserService(repository.Object, new Mock<IAvatarStorage>().Object);
        var result = await service.SearchUsersAsync("  tofu  ", 0, 10, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Items);
        Assert.Equal("tofu_lover", result.Data.Items.First().Username);
        Assert.Equal(1, result.Data.PageIndex);
        repository.Verify(x => x.SearchUsersAsync("tofu", 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchUsersAsync_AcceptsOneCharacterKeyword()
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(x => x.SearchUsersAsync("a", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((0L, (IReadOnlyList<UserSearchProjection>)new List<UserSearchProjection>()));
        var service = new UserService(repository.Object, new Mock<IAvatarStorage>().Object);

        var result = await service.SearchUsersAsync("a", 1, 10, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data!.Items);
        repository.Verify(x => x.SearchUsersAsync("a", 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPublicProfileAsync_MapsPublishedPostStatistics()
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(x => x.GetPublicProfileAsync(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicUserProfileProjection
            {
                Id = 12,
                Username = "vegan_user",
                DisplayName = "Vegan User",
                DietType = "vegan",
                JoinedAt = new DateTime(2026, 1, 1),
                PublishedPostCount = 2,
                ReceivedLikeCount = 9,
                Posts = new List<PostFeedProjection>
                {
                    new() { Id = 101, Title = "Tofu Bowl", PostType = "recipe" }
                }
            });

        var service = new UserService(repository.Object, new Mock<IAvatarStorage>().Object);
        var result = await service.GetPublicProfileAsync(12, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.PublishedPostCount);
        Assert.Equal(9, result.Data.ReceivedLikeCount);
        Assert.Single(result.Data.Posts);
        Assert.Equal(101, result.Data.Posts.First().Id);
    }

    [Fact]
    public async Task GetSavedPostsAsync_MapsNewestFirstPage()
    {
        var repository = new Mock<IPostInteractionRepository>();
        IReadOnlyList<SavedPostProjection> projections = new List<SavedPostProjection>
        {
            new()
            {
                PostId = 50,
                Title = "Saved Curry",
                PostType = "recipe",
                AuthorName = "Chef",
                SavedAt = new DateTime(2026, 2, 1)
            }
        };
        repository.Setup(x => x.GetSavedPostsAsync(3, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((1L, projections));

        var service = new PostInteractionService(repository.Object);
        var result = await service.GetSavedPostsAsync(3, 0, 10, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(50, result.Items.First().PostId);
        Assert.Equal("Saved Curry", result.Items.First().Title);
        Assert.Equal(1, result.PageIndex);
        repository.Verify(x => x.GetSavedPostsAsync(3, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchPostsAsync_AcceptsOneCharacterKeyword()
    {
        var repository = new Mock<IPostRepository>();
        repository.Setup(x => x.SearchPostsAsync("a", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((0L, (IEnumerable<PostFeedProjection>)new List<PostFeedProjection>()));
        var service = CreatePostService(repository);

        var result = await service.SearchPostsAsync(new SearchPostsRequest { Keyword = "a" });

        Assert.Empty(result.Items);
        repository.Verify(x => x.SearchPostsAsync("a", 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("tofu")]
    [InlineData("t")]
    public async Task SearchPostsAsync_TrimsKeywordAndMapsPagedResults(string keyword)
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

        repository.Setup(x => x.SearchPostsAsync(keyword, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((1L, projections));

        var result = await service.SearchPostsAsync(new SearchPostsRequest
        {
            Keyword = $"  {keyword}  ",
            PageIndex = 0,
            PageSize = 10
        });

        Assert.Single(result.Items);
        Assert.Equal(10, result.Items.First().Id);
        Assert.Equal(1L, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
        repository.Verify(x => x.SearchPostsAsync(keyword, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
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

    [Fact]
    public async Task SearchUsersAsync_RejectsEmptyKeyword()
    {
        var repository = new Mock<IUserRepository>();
        var service = new UserService(repository.Object, new Mock<IAvatarStorage>().Object);
        var result = await service.SearchUsersAsync(" ", 1, 10, CancellationToken.None);
        Assert.False(result.Succeeded);
        repository.Verify(x => x.SearchUsersAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchPostsAsync_RejectsEmptyKeyword()
    {
        var repository = new Mock<IPostRepository>();
        await Assert.ThrowsAsync<ArgumentException>(() => CreatePostService(repository).SearchPostsAsync(new SearchPostsRequest { Keyword = " " }));
        repository.Verify(x => x.SearchPostsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    private static PostService CreatePostService(Mock<IPostRepository> repository) => new(
        repository.Object,
        new Mock<IValidator<CreatePostRequest>>().Object,
        new Mock<IValidator<GetMyPostsRequest>>().Object,
        new Mock<IValidator<UpdatePostRequest>>().Object,
        new Mock<IMediaStorageService>().Object,
        new Mock<IPostInteractionRepository>().Object);
}
