using VeganHelper.BLL.DTOs.Comments;

namespace VeganHelper.BLL.Services;

public interface ICommentService
{
    Task<CommentDto> CreateCommentAsync(
        long postId,
        long userId,
        CreateCommentRequest request,
        CancellationToken cancellationToken = default);

    Task<CommentDto> UpdateCommentAsync(
        long postId,
        long commentId,
        long userId,
        UpdateCommentRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteCommentAsync(
        long postId,
        long commentId,
        long userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<ReportCommentResponse> ReportCommentAsync(
        long postId,
        long commentId,
        long reporterId,
        ReportCommentRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommentDto>> GetCommentsAsync(
        long postId,
        long? viewerUserId,
        int maxDepth = 2,
        CancellationToken cancellationToken = default);
}
