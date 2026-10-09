using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

    public Task<Comment?> GetCommentForReportAsync(
        long commentId,
        CancellationToken cancellationToken = default) =>
        context.Comments
            .FromSqlInterpolated($"SELECT * FROM comments WHERE id = {commentId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Comment?> GetCommentForUpdateAsync(
        long commentId,
        CancellationToken cancellationToken = default) =>
        context.Comments
            .SingleOrDefaultAsync(comment => comment.Id == commentId, cancellationToken);

    public Task<CommentProjection?> GetCommentProjectionAsync(
        long commentId,
        CancellationToken cancellationToken = default) =>
        ProjectVisibleCommentQuery(includeHidden: true)
            .SingleOrDefaultAsync(comment => comment.Id == commentId, cancellationToken);

    public async Task<IReadOnlyList<CommentProjection>> GetVisibleCommentsAsync(
        long postId,
        CancellationToken cancellationToken = default) =>
        await ProjectVisibleCommentQuery(includeHidden: false, includeDeleted: true)
            .Where(comment => comment.PostId == postId)
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .ToListAsync(cancellationToken);

    public async Task AddCommentAsync(Comment comment, CancellationToken cancellationToken = default) =>
        await context.Comments.AddAsync(comment, cancellationToken);

    public Task<bool> HasPendingUserReportAsync(
        long commentId,
        long reporterId,
        CancellationToken cancellationToken = default) =>
        context.Flags.AnyAsync(
            flag => flag.CommentId == commentId
                && flag.ReporterId == reporterId
                && flag.SourceType == "user"
                && flag.Status == "pending",
            cancellationToken);

    public Task<int> CountPendingUserReportsAsync(
        long commentId,
        CancellationToken cancellationToken = default) =>
        context.Flags
            .Where(flag => flag.CommentId == commentId
                && flag.SourceType == "user"
                && flag.Status == "pending"
                && flag.ReporterId.HasValue)
            .Select(flag => flag.ReporterId!.Value)
            .Distinct()
            .CountAsync(cancellationToken);

    public async Task AddCommentReportAsync(
        Flag report,
        CancellationToken cancellationToken = default) =>
        await context.Flags.AddAsync(report, cancellationToken);

    public Task<IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default) =>
        context.Database.BeginTransactionAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    private IQueryable<CommentProjection> ProjectVisibleCommentQuery(bool includeHidden, bool includeDeleted = false)
    {
        var query = context.Comments.AsNoTracking();

        if (includeDeleted)
        {
            // Deleted comments remain as placeholders so that their replies do not
            // disappear from the conversation tree.
            query = query.Where(comment => comment.Status == "visible" || comment.IsDeleted);
        }
        else
        {
            query = query.Where(comment => !comment.IsDeleted && (includeHidden || comment.Status == "visible"));
        }

        return query.Select(comment => new CommentProjection
        {
            Id = comment.Id,
            PostId = comment.PostId,
            UserId = comment.UserId,
            ParentCommentId = comment.ParentCommentId,
            Content = comment.Content,
            Status = comment.Status,
            IsDeleted = comment.IsDeleted,
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
