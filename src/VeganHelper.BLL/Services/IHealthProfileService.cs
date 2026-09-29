using VeganHelper.BLL.DTOs.HealthProfile;

namespace VeganHelper.BLL.Services;

public interface IHealthProfileService
{
    Task<HealthProfileDto> GetHealthProfileAsync(long userId);
    Task<UpdateHealthProfileResponse> UpdateHealthProfileAsync(long userId, UpdateHealthProfileRequest request);
    Task<BmiCalculationResult?> GetBmiResultAsync(long userId);
    Task<GetBmiHistoryResponse> GetBmiHistoryAsync(long userId);
    Task DeclareAllergiesAsync(long userId, DeclareAllergiesRequest request);
}
