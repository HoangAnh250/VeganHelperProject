using System.ComponentModel.DataAnnotations;
namespace VeganHelper.BLL.DTOs.Notifications;

public sealed class NotificationListRequest
{
    [Range(1, int.MaxValue)] public int PageIndex { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    public bool UnreadOnly { get; set; }
}
public sealed class NotificationDto
{
    public long Id { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string TargetUrl { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
public sealed class PushSubscriptionRequest
{
    [Required, MaxLength(2048)] public string Endpoint { get; set; } = "";
    [Required] public PushKeys Keys { get; set; } = new();
}
public sealed class PushKeys
{
    [Required, MaxLength(100)] public string P256dh { get; set; } = "";
    [Required, MaxLength(30)] public string Auth { get; set; } = "";
}
public sealed record PushSubscriptionResponse(long Id);
public sealed record UnreadCountResponse(int UnreadCount);
public sealed record ReadNotificationsResponse(int UpdatedCount, int UnreadCount);
public sealed record PushConfigurationResponse(bool Enabled, string? PublicKey);
