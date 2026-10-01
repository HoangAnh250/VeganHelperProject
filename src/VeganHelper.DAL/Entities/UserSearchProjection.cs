namespace VeganHelper.DAL.Entities;

public sealed class UserSearchProjection
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

public sealed class PublicUserProfileProjection
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string DietType { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public long PublishedPostCount { get; set; }
    public long ReceivedLikeCount { get; set; }
    public IReadOnlyList<PostFeedProjection> Posts { get; set; } = Array.Empty<PostFeedProjection>();
}
