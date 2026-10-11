namespace VeganHelper.BLL.DTOs.Comments;

public sealed class CommentDto
{
    public long Id { get; set; }
    public long PostId { get; set; }
    public long UserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public long? ParentCommentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public int Depth { get; set; }
    public bool IsMine { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<CommentDto> Replies { get; set; } = new();
}
