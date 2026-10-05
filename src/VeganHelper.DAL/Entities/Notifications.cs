namespace VeganHelper.DAL.Entities;

public sealed class Notification
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string EventKey { get; set; } = "";
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string TargetUrl { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
public sealed class BrowserPushSubscription
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Endpoint { get; set; } = "";
    public string EndpointHash { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}
public sealed class NotificationPushDelivery
{
    public long Id { get; set; }
    public long NotificationId { get; set; }
    public long SubscriptionId { get; set; }
    public string State { get; set; } = "pending";
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public Guid? LeaseToken { get; set; }
    public string? LastErrorCode { get; set; }
    public Notification Notification { get; set; } = null!;
    public BrowserPushSubscription Subscription { get; set; } = null!;
}
