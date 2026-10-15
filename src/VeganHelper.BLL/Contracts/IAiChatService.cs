using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.AI;

namespace VeganHelper.BLL.Contracts;

public interface IAiChatService
{
    Task<ServiceResult<AiChatResponseDto>> SendAsync(
        long? userId,
        AiChatRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AiChatHistoryDto>> GetHistoryAsync(
        long? userId,
        long sessionId,
        Guid? guestSessionId,
        CancellationToken cancellationToken = default);
}
