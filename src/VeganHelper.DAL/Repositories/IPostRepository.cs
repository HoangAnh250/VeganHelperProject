namespace VeganHelper.DAL.Repositories;

using System.Threading;
using System.Threading.Tasks;
using VeganHelper.DAL.Entities;

public interface IPostRepository
{
    Task<Post> CreatePostAsync(Post post, CancellationToken cancellationToken = default);
    Task<Ingredient?> FindIngredientByIdAsync(long ingredientId, CancellationToken cancellationToken = default);
    Task<Ingredient> GetOrCreateIngredientAsync(string name, string unit, CancellationToken cancellationToken = default);
    Task<(long TotalCount, System.Collections.Generic.IEnumerable<PostFeedProjection> Items)> GetFeedAsync(
        int pageIndex,
        int pageSize,
        int? categoryId = null,
        string? difficultyLevel = null,
        string? dietType = null,
        int? prepTimeMax = null,
        CancellationToken cancellationToken = default);
    Task<(long TotalCount, System.Collections.Generic.IEnumerable<PostFeedProjection> Items)> SearchPostsAsync(
        string keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<(Post? Post, string AuthorName)> GetPostDetailAsync(long postId, CancellationToken cancellationToken = default);

    Task<(System.Collections.Generic.List<Post> Posts, int TotalCount)> GetMyPostsAsync(long authorId, string? status, int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    Task<Post?> GetPostForUpdateAsync(long postId, CancellationToken cancellationToken = default);

    Task IncrementViewCountAsync(long postId, CancellationToken cancellationToken = default);
    Task<bool> DeletePostAsync(long postId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task StagePostUpdateRemovalsAsync(Post post, IReadOnlyCollection<PostMedia> media,
        IReadOnlyCollection<PostCategory> categories, IReadOnlyCollection<PostIngredient> ingredients,
        IReadOnlyCollection<PostStep> steps, CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
