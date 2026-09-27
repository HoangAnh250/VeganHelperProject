using VeganHelper.BLL.DTOs;

namespace VeganHelper.BLL.Contracts;

public interface IGoogleTokenValidator
{
    Task<GoogleIdentityInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken);
}
