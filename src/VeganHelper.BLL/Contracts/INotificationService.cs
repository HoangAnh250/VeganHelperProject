using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Notifications;
namespace VeganHelper.BLL.Contracts;

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> ListAsync(long userId, NotificationListRequest request, CancellationToken ct);
    Task<UnreadCountResponse> UnreadCountAsync(long userId, CancellationToken ct);
    Task<ReadNotificationsResponse> MarkReadAsync(long userId, long id, CancellationToken ct);
    Task<ReadNotificationsResponse> MarkAllReadAsync(long userId, CancellationToken ct);
    Task<PushSubscriptionResponse> SubscribeAsync(long userId, PushSubscriptionRequest request, CancellationToken ct);
    Task<bool> UnsubscribeAsync(long userId, long id, CancellationToken ct);
}
public interface INotificationPublisher
{
    Task<long?> NotifyCommentAsync(long commentId, CancellationToken ct = default);
    Task<long?> NotifyPostReviewAsync(long postId, CancellationToken ct = default);
    Task<long?> NotifyMealReminderAsync(long scheduleId, DateOnly date, CancellationToken ct = default);
}
