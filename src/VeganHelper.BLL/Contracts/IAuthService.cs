using VeganHelper.BLL.DTOs;

namespace VeganHelper.BLL.Contracts;

public interface IAuthService
{
    Task<ServiceResult<MessageResponseDto>> BeginRegistrationAsync(RegistrationStartRequestDto request, CancellationToken ct);
    Task<ServiceResult<MessageResponseDto>> StartRegistrationAsync(ResendVerificationRequestDto request, CancellationToken ct);
    Task<ServiceResult<RegistrationProofDto>> VerifyRegistrationAsync(VerifyEmailRequestDto request, CancellationToken ct);
    Task<ServiceResult<MessageResponseDto>> CompleteRegistrationAsync(CompleteRegistrationRequestDto request, CancellationToken ct);
    Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<AuthResponseDto>> LoginWithGoogleAsync(GoogleLoginRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> LinkGoogleAsync(long userId, GoogleLoginRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> RequestGoogleUnlinkAsync(long userId, RequestGoogleUnlinkDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> ConfirmGoogleUnlinkAsync(long userId, ConfirmGoogleUnlinkDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> RequestEmailChangeAsync(long userId, RequestEmailChangeDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> ConfirmCurrentEmailChangeOtpAsync(long userId, ConfirmEmailChangeOtpDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> ConfirmNewEmailAsync(long userId, ConfirmEmailChangeOtpDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> SetPasswordAsync(long userId, SetPasswordRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> ChangePasswordAsync(long userId, ChangePasswordRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<AuthResponseDto>> RefreshAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> LogoutAsync(long userId, LogoutRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken);
    Task<ServiceResult<MessageResponseDto>> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken);
}
