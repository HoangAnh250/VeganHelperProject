namespace VeganHelper.BLL.DTOs.Comments;

public sealed class ReportCommentResponse
{
    public long ReportId { get; init; }
    public long CommentId { get; init; }
    public int PendingReportCount { get; init; }
    public bool CommentHidden { get; init; }
    public string Message { get; init; } = string.Empty;
}
