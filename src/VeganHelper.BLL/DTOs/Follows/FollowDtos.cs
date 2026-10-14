namespace VeganHelper.BLL.DTOs.Follows;

public sealed record FollowStatusDto(
    long UserId,
    bool IsFollowing,
    long FollowersCount,
    long FollowingCount,
    bool NotifyOnNewPost = false);

public sealed record UpdateFollowNotificationsRequest([property: System.Text.Json.Serialization.JsonRequired] bool NotifyOnNewPost);

public sealed record FollowUserDto(
    long Id,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    DateTime FollowedAt);
