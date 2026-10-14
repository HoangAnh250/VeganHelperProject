namespace VeganHelper.BLL.DTOs.Follows;

public sealed record FollowStatusDto(
    long UserId,
    bool IsFollowing,
    long FollowersCount,
    long FollowingCount);

public sealed record FollowUserDto(
    long Id,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    DateTime FollowedAt);
