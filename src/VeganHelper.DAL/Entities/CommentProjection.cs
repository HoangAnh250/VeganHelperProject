namespace VeganHelper.DAL.Entities;

public sealed class CommentProjection
{
    public long Id { get; set; }
    public long PostId { get; set; }
    public long UserId { get; set; }
    public long? ParentCommentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string AuthorName { get; set; } = "Unknown";
    public string? AvatarUrl { get; set; }
}
