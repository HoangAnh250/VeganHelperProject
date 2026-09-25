using VeganHelper.BLL.DTOs;

namespace VeganHelper.BLL.Contracts;

public interface IUserService
{
    Task<ServiceResult<UserProfileDto>> GetProfileAsync(long userId, CancellationToken cancellationToken);
    Task<ServiceResult<UserProfileDto>> UpdateProfileAsync(long userId, UpdateProfileCommand command, CancellationToken cancellationToken);
}
