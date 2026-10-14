using Moq;
using VeganHelper.BLL.Exceptions;
using VeganHelper.BLL.Services;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.UnitTests;

public sealed class FollowServiceTests
{
    [Fact]
    public async Task FollowAsync_WhenFollowingSelf_RejectsWithoutWriting()
    {
        var repository = new Mock<IFollowRepository>();
        repository.Setup(x => x.IsEligibleUserAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new FollowService(repository.Object, Mock.Of<VeganHelper.BLL.Contracts.INotificationPublisher>()).FollowAsync(7, 7));

        repository.Verify(x => x.FindAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FollowAsync_WhenTargetIsUnavailable_ReturnsNotFound()
    {
        var repository = new Mock<IFollowRepository>();
        repository.Setup(x => x.IsEligibleUserAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.IsEligibleUserAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new FollowService(repository.Object, Mock.Of<VeganHelper.BLL.Contracts.INotificationPublisher>()).FollowAsync(7, 8));

        repository.Verify(x => x.AddAsync(It.IsAny<UserFollow>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FollowAsync_WhenRelationshipAlreadyExists_ReturnsConflict()
    {
        var repository = new Mock<IFollowRepository>();
        repository.Setup(x => x.IsEligibleUserAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.IsEligibleUserAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.FindAsync(7, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserFollow { FollowerId = 7, FollowingId = 8 });

        await Assert.ThrowsAsync<ConflictException>(() =>
            new FollowService(repository.Object, Mock.Of<VeganHelper.BLL.Contracts.INotificationPublisher>()).FollowAsync(7, 8));

        repository.Verify(x => x.AddAsync(It.IsAny<UserFollow>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FollowAsync_WhenValid_CreatesRelationshipAndReturnsCounts()
    {
        var repository = new Mock<IFollowRepository>();
        repository.Setup(x => x.IsEligibleUserAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.IsEligibleUserAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.SetupSequence(x => x.FindAsync(7, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserFollow?)null)
            .ReturnsAsync(new UserFollow { FollowerId = 7, FollowingId = 8 });
        repository.Setup(x => x.CountFollowersAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(4);
        repository.Setup(x => x.CountFollowingAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var result = await new FollowService(repository.Object, Mock.Of<VeganHelper.BLL.Contracts.INotificationPublisher>()).FollowAsync(7, 8);

        Assert.True(result.IsFollowing);
        Assert.Equal(8, result.UserId);
        Assert.Equal(4, result.FollowersCount);
        Assert.Equal(3, result.FollowingCount);
        repository.Verify(x => x.AddAsync(
            It.Is<UserFollow>(follow => follow.FollowerId == 7 && follow.FollowingId == 8),
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnfollowAsync_WhenRelationshipDoesNotExist_ReturnsNotFound()
    {
        var repository = new Mock<IFollowRepository>();
        repository.Setup(x => x.IsEligibleUserAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.IsEligibleUserAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.FindAsync(7, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserFollow?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new FollowService(repository.Object, Mock.Of<VeganHelper.BLL.Contracts.INotificationPublisher>()).UnfollowAsync(7, 8));

        repository.Verify(x => x.Remove(It.IsAny<UserFollow>()), Times.Never);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnfollowAsync_WhenRelationshipExists_RemovesItAndReturnsFalse()
    {
        var repository = new Mock<IFollowRepository>();
        var existing = new UserFollow { FollowerId = 7, FollowingId = 8 };
        repository.Setup(x => x.IsEligibleUserAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.IsEligibleUserAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.SetupSequence(x => x.FindAsync(7, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing)
            .ReturnsAsync((UserFollow?)null);
        repository.Setup(x => x.CountFollowersAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(3);
        repository.Setup(x => x.CountFollowingAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var result = await new FollowService(repository.Object, Mock.Of<VeganHelper.BLL.Contracts.INotificationPublisher>()).UnfollowAsync(7, 8);

        Assert.False(result.IsFollowing);
        Assert.Equal(3, result.FollowersCount);
        repository.Verify(x => x.Remove(existing), Times.Once);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetFollowersAsync_NormalizesPaginationAndMapsItems()
    {
        var repository = new Mock<IFollowRepository>();
        repository.Setup(x => x.IsEligibleUserAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.GetFollowersAsync(8, 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((1L, (IReadOnlyList<FollowUserProjection>)new List<FollowUserProjection>
            {
                new() { Id = 7, Username = "tofu_user", DisplayName = "Tofu User", FollowedAt = new DateTime(2026, 1, 1) }
            }));

        var result = await new FollowService(repository.Object, Mock.Of<VeganHelper.BLL.Contracts.INotificationPublisher>()).GetFollowersAsync(8, 0, 100);

        Assert.Equal(1, result.PageIndex);
        Assert.Equal(50, result.PageSize);
        Assert.Equal(1, result.TotalItems);
        Assert.Single(result.Items);
        Assert.Equal("tofu_user", result.Items.First().Username);
        repository.Verify(x => x.GetFollowersAsync(8, 1, 50, It.IsAny<CancellationToken>()), Times.Once);
    }
}
