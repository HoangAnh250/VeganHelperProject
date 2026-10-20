using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public sealed class AdminCommentReportProjection
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

public interface IAdminCommentReportRepository
{
    Task<DatabasePage<AdminCommentReportProjection>> ListAsync(string? status, int pageIndex, int pageSize, CancellationToken ct);
    Task<AdminCommentReportProjection?> FindAsync(long id, CancellationToken ct);
    Task<Flag?> LockReportAsync(long id, CancellationToken ct);
    Task<Comment?> LockCommentAsync(long id, CancellationToken ct);
    Task ResolvePendingAsync(long commentId, string status, long adminId, string note, DateTime now, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
