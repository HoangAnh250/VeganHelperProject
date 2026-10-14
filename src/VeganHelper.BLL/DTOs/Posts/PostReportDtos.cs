using System.ComponentModel.DataAnnotations;
using VeganHelper.BLL.DTOs.Admin;

namespace VeganHelper.BLL.DTOs.Posts;

public sealed class CreatePostReportRequest
{
    [Required, MaxLength(40)] public string ReasonCategory { get; set; } = "";
    [Required, MaxLength(1000)] public string Reason { get; set; } = "";
    [Range(1, int.MaxValue)] public int ExpectedRevision { get; set; }
}
public sealed record PostReportReceipt(long Id, string Status);
public sealed class PostReportListRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string Status { get; set; } = "pending";
    public long? PostId { get; set; }
}
public sealed class AdminPostReportDto
{
    public long Id { get; set; }
    public long? PostId { get; set; }
    public int? PostRevision { get; set; }
    public string Reason { get; set; } = "";
    public string? ReasonCategory { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? ResolutionNote { get; set; }
    public string? ReportedSnapshotJson { get; set; }
}
public sealed record AdminPostReportDetailDto(AdminPostReportDto Report, ModerationPostDetailDto CurrentContent,
    IReadOnlyList<ModerationAssessmentDto> AssessmentHistory);

public sealed class ModerationAssessmentDto
{
    public long Id { get; set; }
    public int Revision { get; set; }
    public Guid RequestId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ResultJson { get; set; } = "{}";
}
