using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;

namespace VeganHelper.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService service) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await service.RegisterAsync(request, cancellationToken));

    [AllowAnonymous]
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await service.VerifyEmailAsync(request, cancellationToken));

    [AllowAnonymous]
    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(ResendVerificationRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await service.ResendVerificationAsync(request, cancellationToken));

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await service.LoginAsync(request, cancellationToken));

    [AllowAnonymous]
    [HttpPost("google")]
    public async Task<IActionResult> LoginWithGoogle(GoogleLoginRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await service.LoginWithGoogleAsync(request, cancellationToken));

    [Authorize]
    [HttpPost("google/link")]
    public async Task<IActionResult> LinkGoogle(GoogleLoginRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return userId is null
            ? Unauthorized()
            : ToActionResult(await service.LinkGoogleAsync(userId.Value, request, cancellationToken));
    }

    [Authorize]
    [HttpPost("google/unlink/request")]
    public async Task<IActionResult> RequestGoogleUnlink(RequestGoogleUnlinkDto request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return userId is null
            ? Unauthorized()
            : ToActionResult(await service.RequestGoogleUnlinkAsync(userId.Value, request, cancellationToken));
    }

    [Authorize]
    [HttpPost("google/unlink/confirm")]
    public async Task<IActionResult> ConfirmGoogleUnlink(ConfirmGoogleUnlinkDto request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return userId is null
            ? Unauthorized()
            : ToActionResult(await service.ConfirmGoogleUnlinkAsync(userId.Value, request, cancellationToken));
    }

    [Authorize]
    [HttpPost("change-email/request")]
    public async Task<IActionResult> RequestEmailChange(RequestEmailChangeDto request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return userId is null
            ? Unauthorized()
            : ToActionResult(await service.RequestEmailChangeAsync(userId.Value, request, cancellationToken));
    }

    [Authorize]
    [HttpPost("change-email/verify-current")]
    public async Task<IActionResult> VerifyCurrentEmailChangeOtp(ConfirmEmailChangeOtpDto request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return userId is null
            ? Unauthorized()
            : ToActionResult(await service.ConfirmCurrentEmailChangeOtpAsync(userId.Value, request, cancellationToken));
    }

    [Authorize]
    [HttpPost("change-email/confirm")]
    public async Task<IActionResult> ConfirmNewEmail(ConfirmEmailChangeOtpDto request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return userId is null
            ? Unauthorized()
            : ToActionResult(await service.ConfirmNewEmailAsync(userId.Value, request, cancellationToken));
    }

    [Authorize]
    [HttpPost("set-password")]
    public async Task<IActionResult> SetPassword(SetPasswordRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return userId is null
            ? Unauthorized()
            : ToActionResult(await service.SetPasswordAsync(userId.Value, request, cancellationToken));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return userId is null
            ? Unauthorized()
            : ToActionResult(await service.ChangePasswordAsync(userId.Value, request, cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await service.RefreshAsync(request, cancellationToken));

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return userId is null
            ? Unauthorized()
            : ToActionResult(await service.LogoutAsync(userId.Value, request, cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await service.ForgotPasswordAsync(request, cancellationToken));

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await service.ResetPasswordAsync(request, cancellationToken));

    private long? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(value, out var id) ? id : null;
    }

    private IActionResult ToActionResult<T>(ServiceResult<T> result) =>
        result.Succeeded ? StatusCode(result.StatusCode, result.Data) : StatusCode(result.StatusCode, new { error = result.Error });
}
