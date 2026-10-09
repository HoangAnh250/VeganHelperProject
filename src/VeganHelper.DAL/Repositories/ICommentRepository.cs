using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public interface ICommentRepository
{
    Task<bool> IsPublishedPostAsync(long postId, CancellationToken cancellationToken = default);
    Task<Comment?> GetCommentAsync(long commentId, CancellationToken cancellationToken = default);
    Task<CommentProjection?> GetCommentProjectionAsync(long commentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CommentProjection>> GetVisibleCommentsAsync(long postId, CancellationToken cancellationToken = default);
    Task AddCommentAsync(Comment comment, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
