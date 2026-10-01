using VeganHelper.BLL.DTOs.Posts;

namespace VeganHelper.BLL.Services;

public interface IPostInteractionService
{
    Task<ToggleLikeResponse> ToggleLikeAsync(long userId, long postId, CancellationToken cancellationToken = default);
    Task<ToggleSaveResponse> ToggleSaveAsync(long userId, long postId, CancellationToken cancellationToken = default);
}
