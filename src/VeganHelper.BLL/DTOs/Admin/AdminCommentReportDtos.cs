using System.ComponentModel.DataAnnotations;

namespace VeganHelper.BLL.DTOs.Admin;

public sealed class AdminCommentReportListRequest
{
    public int PageIndex { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string Status { get; init; } = "pending";
}

public sealed class AdminCommentReportDecisionRequest
{
    [Required] public string Action { get; init; } = "";
    [Required, StringLength(1000)] public string Reason { get; init; } = "";
}

public sealed class AdminCommentReportDto
{
    public long Id { get; init; }
    public long CommentId { get; init; }
    public long PostId { get; init; }
    public string PostTitle { get; init; } = "";
    public long AuthorId { get; init; }
    public string AuthorName { get; init; } = "";
    public string CommentContent { get; init; } = "";
    public long ReporterId { get; init; }
    public string ReporterName { get; init; } = "";
    public string Reason { get; init; } = "";
    public string Status { get; init; } = "";
    public int ReportCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public string? ResolutionNote { get; init; }
}
