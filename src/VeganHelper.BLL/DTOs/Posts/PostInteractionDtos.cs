namespace VeganHelper.BLL.DTOs.Posts;

public sealed record ToggleLikeResponse(
    long PostId,
    bool IsLiked,
    long LikeCount);

public sealed record ToggleSaveResponse(
    long PostId,
    bool IsSaved);

public sealed class SavedPostItemDto
{
    public long PostId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string PostType { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public long ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime SavedAt { get; set; }
}
