namespace VeganHelper.DAL.Repositories;

using System.Threading;
using System.Threading.Tasks;
using VeganHelper.DAL.Models;

public interface IPostRepository
{
    Task<Post> CreatePostAsync(Post post, CancellationToken cancellationToken = default);
    Task<(long TotalCount, System.Collections.Generic.IEnumerable<PostFeedProjection> Items)> GetFeedAsync(
        int pageIndex,
        int pageSize,
        int? categoryId = null,
        string? difficultyLevel = null,
        string? dietType = null,
        int? prepTimeMax = null,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
