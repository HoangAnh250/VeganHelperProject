using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VeganHelper.BLL.DTOs.Notifications;
using VeganHelper.BLL.Exceptions;
using VeganHelper.BLL.Mapping;
using VeganHelper.BLL.Services;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Integrations;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.UnitTests;

public sealed class NotificationServiceTests
{
    private readonly Mock<INotificationRepository> repository = new();
    private NotificationService Service()
    {
        var config = new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance);
        config.AssertConfigurationIsValid();
        return new(repository.Object, config.CreateMapper(), TimeProvider.System);
    }
    [Theory]
    [InlineData("http://fcm.googleapis.com/push")]
    [InlineData("https://127.0.0.1/push")]
    [InlineData("https://fcm.googleapis.com.evil.example/push")]
    [InlineData("https://evil.example/fcm.googleapis.com")]
    [InlineData("https://fcm.googleapis.com:8443/push")]
    [InlineData("https://user:password@fcm.googleapis.com/push")]
    [InlineData("file:///etc/passwd")]
    public void PushEndpoint_RejectsUnsafeDestinations(string url) => Assert.False(PushSubscriptionValidation.IsAllowedEndpoint(url));
    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/test")]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/test")]
    [InlineData("https://web.push.apple.com/test")]
    [InlineData("https://db5.notify.windows.com/test")]
    public void PushEndpoint_AllowsSupportedProviders(string url) => Assert.True(PushSubscriptionValidation.IsAllowedEndpoint(url));
    [Fact]
    public async Task ReadOtherUsersNotification_ReturnsNotFound()
    {
        repository.Setup(r => r.MarkReadAsync(1, 99, It.IsAny<DateTime>(), default)).ReturnsAsync((false, 0));
        await Assert.ThrowsAsync<NotFoundException>(() => Service().MarkReadAsync(1, 99, default));
    }
    [Fact]
    public async Task OwnComment_DoesNotCreateNotification()
    {
        repository.Setup(r => r.CommentSourceAsync(1, default)).ReturnsAsync(new CommentNotificationSource(7, 7, 2));
        Assert.Null(await Service().NotifyCommentAsync(1));
        repository.Verify(r => r.PublishAsync(It.IsAny<Notification>(), default), Times.Never);
    }
    [Fact]
    public async Task CommentPublisher_UsesOwnerAndStableEventKey()
    {
        repository.Setup(r => r.CommentSourceAsync(8, default)).ReturnsAsync(new CommentNotificationSource(7, 4, 2));
        repository.Setup(r => r.PublishAsync(It.IsAny<Notification>(), default)).ReturnsAsync(10);
        Assert.Equal(10, await Service().NotifyCommentAsync(8));
        repository.Verify(r => r.PublishAsync(It.Is<Notification>(n => n.UserId == 7 && n.EventKey == "comment:8" && n.TargetUrl == "/posts/2"), default), Times.Once);
    }
    [Fact]
    public async Task Pagination_RejectsOversizedPage()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service().ListAsync(1, new NotificationListRequest { PageSize = 101 }, default));
        repository.Verify(r => r.ListAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), default), Times.Never);
    }
    [Theory]
    [InlineData("saved", 1, "2026-10-05", true)]
    [InlineData("saved", 2, "2026-10-05", false)]
    [InlineData("draft", 1, "2026-10-05", false)]
    [InlineData("saved", 1, "2026-10-12", false)]
    public async Task MealReminder_RequiresSavedPlanMatchingDateAndDay(string status, byte day, string dateText, bool expected)
    {
        repository.Setup(r => r.MealSourceAsync(8, default)).ReturnsAsync(new MealReminderSource(7, day,
         new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 11), status));
        repository.Setup(r => r.PublishAsync(It.IsAny<Notification>(), default)).ReturnsAsync(10);
        var result = await Service().NotifyMealReminderAsync(8, DateOnly.Parse(dateText));
        if (expected)
        {
            Assert.Equal(10, result);
            repository.Verify(r => r.PublishAsync(It.Is<Notification>(n => n.UserId == 7 && n.EventKey == "meal:8:2026-10-05" && n.Type == "meal_reminder"), default), Times.Once);
        }
        else Assert.Null(result);
    }
    [Theory]
    [InlineData(201, 1, "sent")]
    [InlineData(410, 1, "expired")]
    [InlineData(404, 1, "expired")]
    [InlineData(403, 1, "failed")]
    [InlineData(429, 1, "pending")]
    [InlineData(503, 4, "pending")]
    [InlineData(503, 5, "failed")]
    public async Task Dispatcher_HandlesSuccessExpiryAndBoundedRetries(int status, int attempts, string expected)
    {
        var delivery = new NotificationPushDelivery
        {
            Id = 1,
            Attempts = attempts,
            Notification = new Notification { UserId = 7 },
            Subscription = new BrowserPushSubscription { UserId = 7, IsActive = true }
        };
        repository.Setup(r => r.ClaimPushAsync(It.IsAny<DateTime>(), It.IsAny<Guid>(), default)).ReturnsAsync([delivery]);
        var transport = new Mock<IWebPushTransport>();
        transport.Setup(t => t.SendAsync(delivery.Subscription, delivery.Notification, It.IsAny<WebPushSettings>(), default)).ReturnsAsync(status);
        var service = new NotificationPushDispatcher(repository.Object, transport.Object, Options.Create(new WebPushSettings { Enabled = true }),
         TimeProvider.System, NullLogger<NotificationPushDispatcher>.Instance);
        await service.DispatchAsync(default);
        repository.Verify(r => r.FinishPushAsync(delivery, expected, It.IsAny<string?>(), It.IsAny<DateTime>(), default), Times.Once);
    }
    [Fact]
    public async Task CancelledDispatch_DoesNotAcknowledgeOrHideCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var delivery = new NotificationPushDelivery { Attempts = 1, Notification = new Notification { UserId = 1 }, Subscription = new BrowserPushSubscription { UserId = 1, IsActive = true } };
        repository.Setup(r => r.ClaimPushAsync(It.IsAny<DateTime>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([delivery]);
        var transport = new Mock<IWebPushTransport>();
        transport.Setup(t => t.SendAsync(It.IsAny<BrowserPushSubscription>(), It.IsAny<Notification>(), It.IsAny<WebPushSettings>(), It.IsAny<CancellationToken>()))
         .Callback(() => cancellation.Cancel()).ThrowsAsync(new OperationCanceledException());
        var service = new NotificationPushDispatcher(repository.Object, transport.Object, Options.Create(new WebPushSettings { Enabled = true }),
         TimeProvider.System, NullLogger<NotificationPushDispatcher>.Instance);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DispatchAsync(cancellation.Token));
        repository.Verify(r => r.FinishPushAsync(It.IsAny<NotificationPushDelivery>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
