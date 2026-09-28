using VeganHelper.BLL.DTOs.HealthProfile;

namespace VeganHelper.BLL.Services;

public interface IHealthProfileService
{
    Task<HealthProfileDto> GetHealthProfileAsync(long userId);
    Task<UpdateHealthProfileResponse> UpdateHealthProfileAsync(long userId, UpdateHealthProfileRequest request);
    Task DeclareAllergiesAsync(long userId, DeclareAllergiesRequest request);
}
