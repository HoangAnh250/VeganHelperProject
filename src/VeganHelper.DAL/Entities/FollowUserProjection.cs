namespace VeganHelper.DAL.Entities;

public sealed class FollowUserProjection
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime FollowedAt { get; set; }
}
