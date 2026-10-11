using Microsoft.EntityFrameworkCore.Storage;
using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public interface IPostReportRepository
{
    Task<IDbContextTransaction> BeginAsync(CancellationToken ct);
    Task<User?> LockReporterAsync(long id, CancellationToken ct);
    Task<Flag?> PendingAsync(long reporterId, long postId, int revision, CancellationToken ct);
    Task<int> RecentCountAsync(long reporterId, DateTime since, CancellationToken ct);
    Task<DatabasePage<Flag>> ListAsync(string? status, long? postId, int page, int size, CancellationToken ct);
    Task<Flag?> FindAsync(long id, bool forUpdate, CancellationToken ct);
    Task<List<PostModerationAssessment>> HistoryAsync(long postId, CancellationToken ct);
    Task<Post?> PostAsync(long id, CancellationToken ct);
    void Add(Flag report);
    Task RequestRecheckAsync(Post post, DateTime now, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
