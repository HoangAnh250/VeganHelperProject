using FluentValidation;
using VeganHelper.BLL.DTOs.Comments;
using VeganHelper.BLL.Exceptions;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class CommentService(
    ICommentRepository repository,
    IValidator<CreateCommentRequest> validator,
    ICommentKeywordFilter keywordFilter) : ICommentService
{
    private const int MinimumDepth = 1;
    private const int PreviewDepth = 2;
    private const int MaximumDepth = 4;

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
        Content = comment.Content,
        Status = comment.Status,
        Depth = depth,
        IsMine = viewerUserId.HasValue && viewerUserId.Value == comment.UserId,
        CreatedAt = comment.CreatedAt,
        UpdatedAt = comment.UpdatedAt
    };
}
