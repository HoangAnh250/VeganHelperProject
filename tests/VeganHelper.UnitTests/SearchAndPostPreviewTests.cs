using FluentValidation;
using Moq;
using VeganHelper.BLL.DTOs.Posts;
using VeganHelper.BLL.Services;
using VeganHelper.BLL.Services.Media;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;
using VeganHelper.DAL.Storage;

namespace VeganHelper.UnitTests;

public sealed class SearchAndPostPreviewTests
{
    private static PostService Service(Mock<IPostRepository> posts, IAdminAuditRepository? access = null) =>
        new(posts.Object, Mock.Of<IValidator<CreatePostRequest>>(),
            Mock.Of<IValidator<GetMyPostsRequest>>(), Mock.Of<IValidator<UpdatePostRequest>>(),
            Mock.Of<IMediaStorageService>(), Mock.Of<IPostInteractionRepository>(),
            adminAccessRepository: access);

    [Theory]
    [InlineData("pending_review", false, true, true)]
    [InlineData("hidden", false, true, true)]
    [InlineData("rejected", false, true, true)]
    [InlineData("published", true, true, true)]
    [InlineData("pending_review", false, false, false)]
    [InlineData("pending_review", true, false, false)]
    public async Task PrivatePost_OnlyActiveAdminCanPreviewAnotherAuthorsPost(
        string status, bool hold, bool activeAdmin, bool allowed)
    {
        var posts = new Mock<IPostRepository>();
        posts.Setup(p => p.GetPostDetailAsync(162, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new Post { Id = 162, AuthorId = 10, Status = status, ModerationHold = hold }, "Author"));
        var access = new Mock<IAdminAuditRepository>();
        access.Setup(a => a.IsActiveAdminAsync(99, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(activeAdmin);
        var service = Service(posts, access.Object);
        if (allowed) Assert.Equal(162, (await service.GetPostDetailAsync(162, 99)).Id);
        else await Assert.ThrowsAsync<VeganHelper.BLL.Exceptions.NotFoundException>(() => service.GetPostDetailAsync(162, 99));
        posts.Verify(p => p.IncrementViewCountAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(11L)]
    public async Task PrivatePost_GuestAndOtherMembersReceiveNotFound(long? viewerId)
    {
        var posts = new Mock<IPostRepository>();
        posts.Setup(p => p.GetPostDetailAsync(162, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new Post { Id = 162, AuthorId = 10, Status = "pending_review" }, "Author"));
        await Assert.ThrowsAsync<VeganHelper.BLL.Exceptions.NotFoundException>(() => Service(posts).GetPostDetailAsync(162, viewerId));
    }

    [Fact]
    public async Task PrivatePost_AuthorCanPreviewButDeletedPostStaysUnavailableToAdmin()
    {
        var posts = new Mock<IPostRepository>();
        var post = new Post { Id = 162, AuthorId = 10, Status = "pending_review" };
        posts.Setup(p => p.GetPostDetailAsync(162, It.IsAny<CancellationToken>())).ReturnsAsync((post, "Author"));
        Assert.Equal(162, (await Service(posts).GetPostDetailAsync(162, 10)).Id);
        post.IsDeleted = true;
        var access = new Mock<IAdminAuditRepository>();
        access.Setup(a => a.IsActiveAdminAsync(99, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await Assert.ThrowsAsync<VeganHelper.BLL.Exceptions.NotFoundException>(() => Service(posts, access.Object).GetPostDetailAsync(162, 99));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  tofu  ")]
    public async Task PostSearch_DefaultBrowseAndSearchPassCategoryAndCapPageSize(string? keyword)
    {
        var posts = new Mock<IPostRepository>();
        posts.Setup(p => p.SearchPostsAsync(keyword == null ? "" : "tofu", 1, 50, It.IsAny<CancellationToken>(), 7, "easy"))
            .ReturnsAsync((0L, Array.Empty<PostFeedProjection>()));
        var page = await Service(posts).SearchPostsAsync(new SearchPostsRequest { Keyword = keyword, CategoryId = 7, DifficultyLevel = " EASY ", PageIndex = 0, PageSize = 100 });
        Assert.Equal(50, page.PageSize);
        posts.Verify(p => p.SearchPostsAsync(keyword == null ? "" : "tofu", 1, 50, It.IsAny<CancellationToken>(), 7, "easy"), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task ExplicitEmptySearchIsRejectedForPostsAndPeople(string keyword)
    {
        var posts = new Mock<IPostRepository>();
        await Assert.ThrowsAsync<ArgumentException>(() => Service(posts).SearchPostsAsync(new SearchPostsRequest { Keyword = keyword }));
        var users = new UserService(Mock.Of<IUserRepository>(), Mock.Of<IAvatarStorage>());
        var result = await users.SearchUsersAsync(keyword, 1, 10, default);
        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task People_DefaultBrowseUsesRepositoryWithoutKeyword()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.SearchUsersAsync("", 1, 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync((0L, (IReadOnlyList<UserSearchProjection>)Array.Empty<UserSearchProjection>()));
        var result = await new UserService(users.Object, Mock.Of<IAvatarStorage>()).SearchUsersAsync(null, 1, 12, default);
        Assert.True(result.Succeeded);
        users.Verify(u => u.SearchUsersAsync("", 1, 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task PostSearchRejectsInvalidCategory(int categoryId) =>
        await Assert.ThrowsAsync<ArgumentException>(() => Service(new()).SearchPostsAsync(new SearchPostsRequest { CategoryId = categoryId }));

    [Theory]
    [InlineData("expert")]
    [InlineData("easy,hard")]
    [InlineData("<script>")]
    public async Task SearchRejectsUnsupportedDifficulty(string difficulty) =>
        await Assert.ThrowsAsync<ArgumentException>(() => Service(new()).SearchPostsAsync(new SearchPostsRequest { DifficultyLevel = difficulty }));
}
