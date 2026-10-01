using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public interface IPostInteractionRepository
{
    Task<bool> IsPublishedPostAsync(long postId, CancellationToken cancellationToken = default);

    Task<PostLike?> FindLikeAsync(long userId, long postId, CancellationToken cancellationToken = default);
    Task AddLikeAsync(PostLike like, CancellationToken cancellationToken = default);
    void RemoveLike(PostLike like);
    Task<long> CountLikesAsync(long postId, CancellationToken cancellationToken = default);

    Task<SavedPost?> FindSavedPostAsync(long userId, long postId, CancellationToken cancellationToken = default);
    Task AddSavedPostAsync(SavedPost savedPost, CancellationToken cancellationToken = default);
    void RemoveSavedPost(SavedPost savedPost);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
