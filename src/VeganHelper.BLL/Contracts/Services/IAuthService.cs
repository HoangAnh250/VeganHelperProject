using VeganHelper.BLL.DTOs;

namespace VeganHelper.BLL.Contracts.Services;

public interface IAuthService
{
    Task<ServiceResult<MessageResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<AuthResponseDto>> RefreshAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> LogoutAsync(long userId, LogoutRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken);
}
