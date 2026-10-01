using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class PostInteractionRepository(AppDbContext context) : IPostInteractionRepository
{
    public Task<bool> IsPublishedPostAsync(long postId, CancellationToken cancellationToken = default) =>
        context.Posts.AnyAsync(
            post => post.Id == postId && post.Status == "published" && !post.IsDeleted,
            cancellationToken);

    public Task<PostLike?> FindLikeAsync(long userId, long postId, CancellationToken cancellationToken = default) =>
        context.PostLikes.SingleOrDefaultAsync(
            like => like.UserId == userId && like.PostId == postId,
            cancellationToken);

    public Task AddLikeAsync(PostLike like, CancellationToken cancellationToken = default) =>
        context.PostLikes.AddAsync(like, cancellationToken).AsTask();

    public void RemoveLike(PostLike like) => context.PostLikes.Remove(like);

    public Task<long> CountLikesAsync(long postId, CancellationToken cancellationToken = default) =>
        context.PostLikes.LongCountAsync(like => like.PostId == postId, cancellationToken);

    public Task<SavedPost?> FindSavedPostAsync(long userId, long postId, CancellationToken cancellationToken = default) =>
        context.SavedPosts.SingleOrDefaultAsync(
            savedPost => savedPost.UserId == userId && savedPost.PostId == postId,
            cancellationToken);

    public Task AddSavedPostAsync(SavedPost savedPost, CancellationToken cancellationToken = default) =>
        context.SavedPosts.AddAsync(savedPost, cancellationToken).AsTask();

    public void RemoveSavedPost(SavedPost savedPost) => context.SavedPosts.Remove(savedPost);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
