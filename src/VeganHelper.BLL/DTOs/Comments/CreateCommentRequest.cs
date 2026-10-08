namespace VeganHelper.BLL.DTOs.Comments;

public sealed class CreateCommentRequest
{
    public long? ParentCommentId { get; set; }
    public string Content { get; set; } = string.Empty;
}
