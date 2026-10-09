using Microsoft.EntityFrameworkCore.Storage;
using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public interface ICommentRepository
{
    Task<bool> IsPublishedPostAsync(long postId, CancellationToken cancellationToken = default);
    Task<Comment?> GetCommentAsync(long commentId, CancellationToken cancellationToken = default);
    Task<Comment?> GetCommentForReportAsync(long commentId, CancellationToken cancellationToken = default);
    Task<Comment?> GetCommentForUpdateAsync(long commentId, CancellationToken cancellationToken = default);
    Task<CommentProjection?> GetCommentProjectionAsync(long commentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CommentProjection>> GetVisibleCommentsAsync(long postId, CancellationToken cancellationToken = default);
    Task AddCommentAsync(Comment comment, CancellationToken cancellationToken = default);
    Task<bool> HasPendingUserReportAsync(long commentId, long reporterId, CancellationToken cancellationToken = default);
    Task<int> CountPendingUserReportsAsync(long commentId, CancellationToken cancellationToken = default);
    Task AddCommentReportAsync(Flag report, CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
