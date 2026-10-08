using VeganHelper.BLL.DTOs.Comments;

namespace VeganHelper.BLL.Services;

public interface ICommentService
{
    Task<CommentDto> CreateCommentAsync(
        long postId,
        long userId,
        CreateCommentRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommentDto>> GetCommentsAsync(
        long postId,
        long? viewerUserId,
        int maxDepth = 2,
        CancellationToken cancellationToken = default);
}
