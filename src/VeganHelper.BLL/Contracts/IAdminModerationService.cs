using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;

namespace VeganHelper.BLL.Contracts;

public interface IAdminModerationService
{
    Task<PagedResult<ModerationPostDto>> ListAsync(AdminActor actor, ModerationPostListRequest request, CancellationToken ct);
    Task<ModerationPostDetailDto> GetAsync(AdminActor actor, long id, CancellationToken ct);
    Task<PagedResult<ModerationFlagDto>> FlagsAsync(AdminActor actor, ModerationFlagListRequest request, CancellationToken ct);
    Task<ModerationDecisionDto> DecideAsync(AdminActor actor, long postId, ModerationDecisionRequest request, CancellationToken ct);
    Task<ModerationDecisionDto> DecideFlagAsync(AdminActor actor, long flagId, ModerationDecisionRequest request, CancellationToken ct);
    Task<ModerationSettingsDto> SettingsAsync(AdminActor actor, CancellationToken ct);
    Task<ModerationSettingsDto> UpdateSettingsAsync(AdminActor actor, ModerationSettingsRequest request, CancellationToken ct);
}
