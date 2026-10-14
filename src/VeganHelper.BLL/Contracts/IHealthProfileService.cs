using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.HealthProfile;

namespace VeganHelper.BLL.Services;

public interface IHealthProfileService
{
    Task<HealthProfileDto> GetHealthProfileAsync(long userId, CancellationToken ct = default);
    Task<UpdateHealthProfileResponse> UpdateHealthProfileAsync(long userId, UpdateHealthProfileRequest request, CancellationToken ct = default);
    Task<BmiCalculationResult> GetBmiResultAsync(long userId, CancellationToken ct = default);
    Task<GetBmiHistoryResponse> GetBmiHistoryAsync(long userId, int pageIndex = 1, int pageSize = 100, CancellationToken ct = default);
    Task DeclareAllergiesAsync(long userId, DeclareAllergiesRequest request, CancellationToken ct = default);
    Task<PagedResult<IngredientOptionDto>> GetIngredientsAsync(string? keyword, int pageIndex = 1, int pageSize = 20, CancellationToken ct = default);
    Task<AllergyWarningsDto> GetPostWarningsAsync(long userId, long postId, CancellationToken ct = default);
}
