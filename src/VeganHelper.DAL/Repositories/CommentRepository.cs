using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class CommentRepository(AppDbContext context) : ICommentRepository
{
    public Task<bool> IsPublishedPostAsync(
        long postId,
        CancellationToken cancellationToken = default) =>
        context.Posts.AnyAsync(
            post => post.Id == postId && post.Status == "published" && !post.IsDeleted,
            cancellationToken);

    public Task<Comment?> GetCommentAsync(
        long commentId,
        CancellationToken cancellationToken = default) =>
        context.Comments.AsNoTracking()
            .SingleOrDefaultAsync(comment => comment.Id == commentId, cancellationToken);

    public Task<CommentProjection?> GetCommentProjectionAsync(
        long commentId,
        CancellationToken cancellationToken = default) =>
        ProjectVisibleCommentQuery(includeHidden: true)
            .SingleOrDefaultAsync(comment => comment.Id == commentId, cancellationToken);

    public async Task<IReadOnlyList<CommentProjection>> GetVisibleCommentsAsync(
        long postId,
        CancellationToken cancellationToken = default) =>
        await ProjectVisibleCommentQuery(includeHidden: false)
            .Where(comment => comment.PostId == postId)
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .ToListAsync(cancellationToken);

    public async Task AddCommentAsync(Comment comment, CancellationToken cancellationToken = default) =>
        await context.Comments.AddAsync(comment, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    private IQueryable<CommentProjection> ProjectVisibleCommentQuery(bool includeHidden)
    {
        var query = context.Comments.AsNoTracking()
            .Where(comment => !comment.IsDeleted);

        if (!includeHidden)
        {
            query = query.Where(comment => comment.Status == "visible");
        }

        return query.Select(comment => new CommentProjection
        {
            Id = comment.Id,
            PostId = comment.PostId,
            UserId = comment.UserId,
            ParentCommentId = comment.ParentCommentId,
            Content = comment.Content,
            Status = comment.Status,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt,
            AuthorName = context.UserProfiles
                .Where(profile => profile.UserId == comment.UserId)
                .Select(profile => profile.DisplayName)
                .FirstOrDefault()
                ?? context.Users
                    .Where(user => user.Id == comment.UserId)
                    .Select(user => user.Username)
                    .FirstOrDefault()
                ?? "Unknown",
            AvatarUrl = context.UserProfiles
                .Where(profile => profile.UserId == comment.UserId)
                .Select(profile => profile.AvatarUrl)
                .FirstOrDefault()
        });
    }
}
