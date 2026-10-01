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

    public async Task<(long TotalCount, IReadOnlyList<SavedPostProjection> Items)> GetSavedPostsAsync(
        long userId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.SavedPosts
            .AsNoTracking()
            .Where(saved => saved.UserId == userId)
            .Join(
                context.Posts.AsNoTracking().Where(post => post.Status == "published" && !post.IsDeleted),
                saved => saved.PostId,
                post => post.Id,
                (saved, post) => new { saved, post });

        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.saved.SavedAt)
            .ThenByDescending(item => item.post.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new SavedPostProjection
            {
                PostId = item.post.Id,
                Title = item.post.Title,
                PostType = item.post.PostType,
                ThumbnailUrl = item.post.Media
                    .Where(media => media.IsPrimary)
                    .Select(media => media.MediaUrl)
                    .FirstOrDefault(),
                AuthorName = context.UserProfiles
                    .Where(profile => profile.UserId == item.post.AuthorId)
                    .Select(profile => profile.DisplayName)
                    .FirstOrDefault()
                    ?? context.Users
                        .Where(author => author.Id == item.post.AuthorId)
                        .Select(author => author.Username)
                        .FirstOrDefault()
                    ?? "Unknown",
                AvatarUrl = context.UserProfiles
                    .Where(profile => profile.UserId == item.post.AuthorId)
                    .Select(profile => profile.AvatarUrl)
                    .FirstOrDefault(),
                ViewCount = item.post.ViewCount,
                CreatedAt = item.post.CreatedAt,
                SavedAt = item.saved.SavedAt
            })
            .ToListAsync(cancellationToken);

        return (totalCount, items);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
