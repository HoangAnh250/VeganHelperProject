using FluentValidation;
using VeganHelper.BLL.DTOs.Comments;
using VeganHelper.BLL.Exceptions;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class CommentService(
    ICommentRepository repository,
    IValidator<CreateCommentRequest> validator,
    IValidator<UpdateCommentRequest> updateValidator,
    IValidator<ReportCommentRequest> reportValidator,
    ICommentKeywordFilter keywordFilter) : ICommentService
{
    private const int MinimumDepth = 1;
    private const int PreviewDepth = 2;
    private const int MaximumDepth = 4;
    private const int ReportThreshold = 3;

    public async Task<CommentDto> CreateCommentAsync(
        long postId,
        long userId,
        CreateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        if (postId <= 0 || userId <= 0)
        {
            throw new ArgumentException("Post ID and user ID must be positive.");
        }

        if (!await repository.IsPublishedPostAsync(postId, cancellationToken))
        {
            throw new NotFoundException($"Published post with ID {postId} was not found.");
        }

        var content = request.Content.Trim();
        var moderation = keywordFilter.Evaluate(content);
        if (moderation.Action == CommentModerationAction.BlockRejected)
        {
            throw new ArgumentException("Comment contains blocked language and was not posted.");
        }

        var depth = MinimumDepth;
        if (request.ParentCommentId.HasValue)
        {
            var parent = await repository.GetCommentAsync(request.ParentCommentId.Value, cancellationToken);
            if (parent is null || parent.IsDeleted || parent.Status != "visible")
            {
                throw new NotFoundException("The parent comment was not found or is no longer available.");
            }

            if (parent.PostId != postId)
            {
                throw new ArgumentException("The parent comment does not belong to this post.");
            }

            depth = await GetDepthAsync(parent, cancellationToken) + 1;
            if (depth > MaximumDepth)
            {
                throw new ArgumentException("Comments can be nested up to 4 levels.");
            }
        }

        var comment = new Comment
        {
            PostId = postId,
            UserId = userId,
            ParentCommentId = request.ParentCommentId,
            Content = content,
            Status = moderation.Action == CommentModerationAction.HidePendingReview
                ? "hidden"
                : "visible",
            CreatedAt = DateTime.UtcNow
        };

        await repository.AddCommentAsync(comment, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        var projection = await repository.GetCommentProjectionAsync(comment.Id, cancellationToken);
        if (projection is null)
        {
            throw new InvalidOperationException("The comment was saved but could not be loaded.");
        }

        return ToDto(projection, userId, depth);
    }

    public async Task<CommentDto> UpdateCommentAsync(
        long postId,
        long commentId,
        long userId,
        UpdateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (postId <= 0 || commentId <= 0 || userId <= 0)
        {
            throw new ArgumentException("Post ID, comment ID and user ID must be positive.");
        }

        if (!await repository.IsPublishedPostAsync(postId, cancellationToken))
        {
            throw new NotFoundException($"Published post with ID {postId} was not found.");
        }

        var comment = await repository.GetCommentForUpdateAsync(commentId, cancellationToken);
        EnsureCommentBelongsToPost(comment, postId);
        EnsureCommentOwner(comment!, userId);

        var content = request.Content.Trim();
        var moderation = keywordFilter.Evaluate(content);
        if (moderation.Action == CommentModerationAction.BlockRejected)
        {
            throw new ArgumentException("Comment contains blocked language and was not updated.");
        }

        comment!.Content = content;
        comment.Status = moderation.Action == CommentModerationAction.HidePendingReview
            ? "hidden"
            : "visible";
        comment.UpdatedAt = DateTime.UtcNow;

        await repository.SaveChangesAsync(cancellationToken);

        var projection = await repository.GetCommentProjectionAsync(comment.Id, cancellationToken);
        if (projection is null)
        {
            throw new InvalidOperationException("The comment was updated but could not be loaded.");
        }

        var depth = await GetDepthAsync(comment, cancellationToken);
        return ToDto(projection, userId, depth);
    }

    public async Task DeleteCommentAsync(
        long postId,
        long commentId,
        long userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        if (postId <= 0 || commentId <= 0 || userId <= 0)
        {
            throw new ArgumentException("Post ID, comment ID and user ID must be positive.");
        }

        var comment = await repository.GetCommentForUpdateAsync(commentId, cancellationToken);
        EnsureCommentBelongsToPost(comment, postId);
        EnsureCanDeleteComment(comment!, userId, isAdministrator);

        // The existing schema intentionally supports soft deletion. This removes the
        // comment from public reads while preserving replies, foreign keys and audit data.
        comment!.IsDeleted = true;
        comment.DeletedAt = DateTime.UtcNow;
        comment.UpdatedAt = DateTime.UtcNow;
        comment.Status = "hidden";

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReportCommentResponse> ReportCommentAsync(
        long postId,
        long commentId,
        long reporterId,
        ReportCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        await reportValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (postId <= 0 || commentId <= 0 || reporterId <= 0)
        {
            throw new ArgumentException("Post ID, comment ID and reporter ID must be positive.");
        }

        if (!await repository.IsPublishedPostAsync(postId, cancellationToken))
        {
            throw new NotFoundException($"Published post with ID {postId} was not found.");
        }

        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);
        var comment = await repository.GetCommentForReportAsync(commentId, cancellationToken);
        EnsureCommentBelongsToPost(comment, postId);

        if (comment!.Status != "visible")
        {
            throw new ConflictException("This comment is already hidden or under moderation.");
        }

        if (comment.UserId == reporterId)
        {
            throw new ArgumentException("You cannot report your own comment.");
        }

        if (await repository.HasPendingUserReportAsync(commentId, reporterId, cancellationToken))
        {
            throw new ConflictException("You have already reported this comment.");
        }

        var report = new Flag
        {
            ReporterId = reporterId,
            CommentId = commentId,
            Reason = request.Reason.Trim(),
            Status = "pending",
            SourceType = "user",
            CreatedAt = DateTime.UtcNow
        };

        await repository.AddCommentReportAsync(report, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        var pendingReportCount = await repository.CountPendingUserReportsAsync(commentId, cancellationToken);
        var commentHidden = pendingReportCount >= ReportThreshold;
        if (commentHidden)
        {
            comment.Status = "hidden";
            comment.UpdatedAt = DateTime.UtcNow;
            await repository.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new ReportCommentResponse
        {
            ReportId = report.Id,
            CommentId = commentId,
            PendingReportCount = pendingReportCount,
            CommentHidden = commentHidden,
            Message = commentHidden
                ? "Report submitted. The comment was hidden after reaching the report threshold."
                : "Report submitted for review. The comment remains visible for now."
        };
    }

    public async Task<IReadOnlyList<CommentDto>> GetCommentsAsync(
        long postId,
        long? viewerUserId,
        int maxDepth = PreviewDepth,
        CancellationToken cancellationToken = default)
    {
        if (postId <= 0)
        {
            throw new ArgumentException("Post ID must be positive.");
        }

        if (maxDepth < MinimumDepth || maxDepth > MaximumDepth)
        {
            throw new ArgumentException("maxDepth must be between 1 and 4.");
        }

        if (!await repository.IsPublishedPostAsync(postId, cancellationToken))
        {
            throw new NotFoundException($"Published post with ID {postId} was not found.");
        }

        var comments = await repository.GetVisibleCommentsAsync(postId, cancellationToken);
        var childrenByParent = comments
            .Where(comment => comment.ParentCommentId.HasValue)
            .GroupBy(comment => comment.ParentCommentId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).ToList());

        var roots = comments
            .Where(comment => comment.ParentCommentId is null)
            .OrderByDescending(comment => comment.CreatedAt)
            .ThenByDescending(comment => comment.Id)
            .Select(comment => BuildTree(comment, 1, maxDepth, viewerUserId, childrenByParent))
            .ToList();

        return roots;
    }

    private async Task<int> GetDepthAsync(Comment comment, CancellationToken cancellationToken)
    {
        var depth = MinimumDepth;
        var current = comment;
        var visited = new HashSet<long> { comment.Id };

        while (current.ParentCommentId is long parentId)
        {
            if (!visited.Add(parentId))
            {
                throw new ArgumentException("The comment hierarchy is invalid.");
            }

            var parent = await repository.GetCommentAsync(parentId, cancellationToken);
            if (parent is null || parent.IsDeleted || parent.Status != "visible")
            {
                throw new ArgumentException("The comment hierarchy is invalid.");
            }

            if (parent.PostId != comment.PostId)
            {
                throw new ArgumentException("The comment hierarchy is invalid.");
            }

            depth++;
            current = parent;
        }

        return depth;
    }

    private static void EnsureCommentBelongsToPost(Comment? comment, long postId)
    {
        if (comment is null || comment.IsDeleted)
        {
            throw new NotFoundException("The comment was not found or is no longer available.");
        }

        if (comment.PostId != postId)
        {
            throw new NotFoundException("The comment was not found for this post.");
        }
    }

    private static void EnsureCommentOwner(Comment comment, long userId)
    {
        if (comment.UserId != userId)
        {
            throw new UnauthorizedAccessException("Only the comment owner can edit this comment.");
        }
    }

    private static void EnsureCanDeleteComment(Comment comment, long userId, bool isAdministrator)
    {
        if (!isAdministrator && comment.UserId != userId)
        {
            throw new UnauthorizedAccessException("Only the comment owner or an administrator can hide this comment.");
        }
    }

    private static CommentDto BuildTree(
        CommentProjection comment,
        int depth,
        int maxDepth,
        long? viewerUserId,
        IReadOnlyDictionary<long, List<CommentProjection>> childrenByParent)
    {
        var dto = ToDto(comment, viewerUserId, depth);
        if (depth >= maxDepth || !childrenByParent.TryGetValue(comment.Id, out var children))
        {
            return dto;
        }

        dto.Replies = children
            .Select(child => BuildTree(child, depth + 1, maxDepth, viewerUserId, childrenByParent))
            .ToList();
        return dto;
    }

    private static CommentDto ToDto(CommentProjection comment, long? viewerUserId, int depth) => new()
    {
        Id = comment.Id,
        PostId = comment.PostId,
        UserId = comment.UserId,
        AuthorName = comment.AuthorName,
        AvatarUrl = comment.AvatarUrl,
        ParentCommentId = comment.ParentCommentId,
        Content = comment.IsDeleted ? "Comment deleted" : comment.Content,
        Status = comment.IsDeleted ? "deleted" : comment.Status,
        IsDeleted = comment.IsDeleted,
        Depth = depth,
        IsMine = viewerUserId.HasValue && viewerUserId.Value == comment.UserId,
        CreatedAt = comment.CreatedAt,
        UpdatedAt = comment.UpdatedAt
    };
}
