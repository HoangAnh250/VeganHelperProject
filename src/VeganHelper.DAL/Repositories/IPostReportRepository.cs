using Microsoft.EntityFrameworkCore.Storage;
using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public sealed class AdminPostReportProjection
{
    public long Id { get; init; }
    public long? PostId { get; init; }
    public int ReportCount { get; set; }
    public string PostTitle { get; init; } = "";
    public long AuthorId { get; init; }
    public string AuthorName { get; init; } = "";
    public long? ReporterId { get; init; }
    public string ReporterName { get; init; } = "";
    public int? PostRevision { get; init; }
    public string Reason { get; init; } = "";
    public string? ReasonCategory { get; init; }
    public string Status { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public string? ResolutionNote { get; init; }
    public string? ReportedSnapshotJson { get; init; }
}

public interface IPostReportRepository
{
    Task<IDbContextTransaction> BeginAsync(CancellationToken ct);
    Task<User?> LockReporterAsync(long id, CancellationToken ct);
    Task<Flag?> PendingAsync(long reporterId, long postId, int revision, CancellationToken ct);
    Task<int> RecentCountAsync(long reporterId, DateTime since, CancellationToken ct);
    Task<DatabasePage<AdminPostReportProjection>> ListAsync(string? status, long? postId, int page, int size, CancellationToken ct);
    Task<IReadOnlyList<AdminPostReportProjection>> ListRevisionAsync(long postId, int? revision, string status, CancellationToken ct);
    Task<AdminPostReportProjection?> GetReportProjectionAsync(long id, CancellationToken ct);
    Task<Flag?> FindAsync(long id, bool forUpdate, CancellationToken ct);
    Task<List<PostModerationAssessment>> HistoryAsync(long postId, CancellationToken ct);
    Task<Post?> PostAsync(long id, CancellationToken ct);
    void Add(Flag report);
    Task RequestRecheckAsync(Post post, DateTime now, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
