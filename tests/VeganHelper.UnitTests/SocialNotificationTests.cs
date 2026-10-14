using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.Mapping;
using VeganHelper.BLL.Services;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.UnitTests;

public sealed class SocialNotificationTests
{
    private readonly Mock<INotificationRepository> repository = new();
    private NotificationService Service() => new(repository.Object,
        new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper(), TimeProvider.System);

    [Theory]
    [InlineData(7, 4, true)]
    [InlineData(7, 7, false)]
    public async Task Like_NotifiesOnlyOtherAuthor(long author, long actor, bool expected)
    {
        repository.Setup(r => r.LikeSourceAsync(actor, 2, default)).ReturnsAsync(new LikeNotificationSource(author, actor, 2));
        await Service().NotifyPostLikeAsync(actor, 2);
        repository.Verify(r => r.PublishAsync(It.Is<Notification>(n => n.UserId == author && n.Type == "post_like" && n.EventKey == $"like:2:{actor}"), default), expected ? Times.Once() : Times.Never());
    }

    [Theory]
    [InlineData(7, 7, 4, true)] // post owner replies to another user's comment
    [InlineData(7, 4, 4, false)] // replies to own comment
    [InlineData(7, 4, 7, true)] // parent belongs to post owner: only one reply notification
    public async Task Reply_OnlyParentAuthorReceivesNotification(long postAuthor, long actor, long parentAuthor, bool expected)
    {
        repository.Setup(r => r.CommentSourceAsync(8, default)).ReturnsAsync(new CommentNotificationSource(postAuthor, actor, 2, 6, parentAuthor));
        await Service().NotifyCommentAsync(8);
        repository.Verify(r => r.PublishAsync(It.Is<Notification>(n => n.UserId == parentAuthor && n.Type == "comment_reply" && n.EventKey == "reply:8"), default), expected ? Times.Once() : Times.Never());
        repository.Verify(r => r.PublishAsync(It.Is<Notification>(n => n.Type == "comment"), default), Times.Never);
    }

    [Fact]
    public async Task Follow_PublishesAfterRelationshipSavedAndDefaultsOff()
    {
        var follows = new Mock<IFollowRepository>();
        var publisher = new Mock<INotificationPublisher>();
        var tx = new Mock<IDbContextTransaction>();
        follows.Setup(r => r.BeginTransactionAsync(1, 2, default)).ReturnsAsync(tx.Object);
        follows.Setup(r => r.IsEligibleUserAsync(It.IsAny<long>(), default)).ReturnsAsync(true);
        UserFollow? saved = null;
        follows.Setup(r => r.AddAsync(It.IsAny<UserFollow>(), default)).Callback<UserFollow, CancellationToken>((f, _) => saved = f).Returns(Task.CompletedTask);
        await new FollowService(follows.Object, publisher.Object).FollowAsync(1, 2);
        Assert.NotNull(saved);
        Assert.False(saved!.NotifyOnNewPost);
        publisher.Verify(p => p.NotifyFollowAsync(1, 2, default), Times.Once);
        tx.Verify(t => t.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task Unlike_DoesNotPublish()
    {
        var likes = new Mock<IPostInteractionRepository>();
        var publisher = new Mock<INotificationPublisher>();
        likes.Setup(r => r.BeginLikeTransactionAsync(2, default)).ReturnsAsync(new Mock<IDbContextTransaction>().Object);
        likes.Setup(r => r.IsPublishedPostAsync(2, default)).ReturnsAsync(true);
        likes.Setup(r => r.FindLikeAsync(1, 2, default)).ReturnsAsync(new PostLike { UserId = 1, PostId = 2 });
        await new PostInteractionService(likes.Object, publisher.Object).ToggleLikeAsync(1, 2);
        publisher.Verify(p => p.NotifyPostLikeAsync(It.IsAny<long>(), It.IsAny<long>(), default), Times.Never);
    }

    [Fact]
    public async Task Preference_RequiresExistingFollow()
    {
        var follows = new Mock<IFollowRepository>();
        follows.Setup(r => r.IsEligibleUserAsync(It.IsAny<long>(), default)).ReturnsAsync(true);
        follows.Setup(r => r.BeginTransactionAsync(1, 2, default)).ReturnsAsync(new Mock<IDbContextTransaction>().Object);
        await Assert.ThrowsAsync<VeganHelper.BLL.Exceptions.NotFoundException>(() =>
            new FollowService(follows.Object, Mock.Of<INotificationPublisher>()).SetNewPostNotificationsAsync(1, 2, true));
        follows.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }
}
