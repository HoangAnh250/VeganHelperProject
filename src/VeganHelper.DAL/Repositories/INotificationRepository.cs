using VeganHelper.DAL.Entities;
namespace VeganHelper.DAL.Repositories;

public interface INotificationRepository
{
    Task<ModerationNotificationSource?> ModerationDecisionSourceAsync(long id, CancellationToken ct);
    Task<(int Count, List<Notification> Items)> ListAsync(long userId, int page, int size, bool unreadOnly, CancellationToken ct);
    Task<int> UnreadCountAsync(long userId, CancellationToken ct);
    Task<(bool Exists, int Updated)> MarkReadAsync(long userId, long id, DateTime now, CancellationToken ct);
    Task<int> MarkAllReadAsync(long userId, DateTime now, CancellationToken ct);
    Task<long> SubscribeAsync(BrowserPushSubscription subscription, CancellationToken ct);
    Task<bool> UnsubscribeAsync(long userId, long id, CancellationToken ct);
    Task<long> PublishAsync(Notification notification, CancellationToken ct);
    Task<CommentNotificationSource?> CommentSourceAsync(long id, CancellationToken ct);
    Task<Post?> ReviewedPostAsync(long id, CancellationToken ct);
    Task<MealReminderSource?> MealSourceAsync(long scheduleId, CancellationToken ct);
    Task<List<NotificationPushDelivery>> ClaimPushAsync(DateTime now, Guid leaseToken, CancellationToken ct);
    Task FinishPushAsync(NotificationPushDelivery delivery, string state, string? error, DateTime nextAttempt, CancellationToken ct);
}
public sealed record CommentNotificationSource(long AuthorId, long CommenterId, long PostId);
public sealed record ModerationNotificationSource(long AuthorId, long PostId, string Action, string Reason);
public sealed record MealReminderSource(long UserId, byte DayOfWeek, DateOnly? StartDate, DateOnly? EndDate, string Status);
