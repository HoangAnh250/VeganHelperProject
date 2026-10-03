using VeganHelper.BLL.DTOs;

namespace VeganHelper.BLL.Contracts;

public interface IAuthService
{
    Task<ServiceResult<MessageResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> ResendVerificationAsync(ResendVerificationRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<AuthResponseDto>> LoginWithGoogleAsync(GoogleLoginRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> LinkGoogleAsync(long userId, GoogleLoginRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> UnlinkGoogleAsync(long userId, UnlinkGoogleRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> SetPasswordAsync(long userId, SetPasswordRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> ChangePasswordAsync(long userId, ChangePasswordRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<AuthResponseDto>> RefreshAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> LogoutAsync(long userId, LogoutRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken);
}
