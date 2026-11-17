using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.API.Authorization;

namespace VeganHelper.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService service, IConfiguration configuration, LoginFailureLimiter loginFailures) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("otp-send")]
    [HttpPost("register")]
    [HttpPost("resend-verification")]
    [HttpPost("verify-email")]
    public IActionResult LegacyRegistration() =>
        Conflict(new { error = "Registration has changed. Refresh the page to continue." });

    [AllowAnonymous]
    [EnableRateLimiting("otp-send")]
    [HttpPost("register/start")]
    public async Task<IActionResult> StartRegistration(ResendVerificationRequestDto request, CancellationToken ct) =>
        RegistrationEnabled() ? ToActionResult(await service.StartRegistrationAsync(request, ct)) : RegistrationPaused();

    [AllowAnonymous]
    [EnableRateLimiting("otp-verify")]
    [HttpPost("register/verify")]
    public async Task<IActionResult> VerifyRegistration(VerifyEmailRequestDto request, CancellationToken ct) =>
        RegistrationEnabled() ? ToActionResult(await service.VerifyRegistrationAsync(request, ct)) : RegistrationPaused();

    [AllowAnonymous]
    [EnableRateLimiting("otp-verify")]
    [HttpPost("register/complete")]
    public async Task<IActionResult> CompleteRegistration(CompleteRegistrationRequestDto request, CancellationToken ct) =>
        RegistrationEnabled() ? ToActionResult(await service.CompleteRegistrationAsync(request, ct)) : RegistrationPaused();

    private bool RegistrationEnabled() => configuration.GetValue("Registration:Enabled", true);
    private IActionResult RegistrationPaused() =>
        StatusCode(503, new { error = "Registration is temporarily unavailable. Please try again later." });

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await loginFailures.RunAsync(request.Identifier,
            HttpContext.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown",
            () => service.LoginAsync(request, cancellationToken),
            result => result.StatusCode == 401, result => result.Succeeded, seconds =>
            {
                Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return ServiceResult<AuthResponseDto>.Fail("Too many failed sign-in attempts. Please try again later.", 429);
            }, cancellationToken);
        return ToActionResult(result);
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
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
    [EnableRateLimiting("otp-send")]
    [HttpPost("google/unlink/request")]
    public async Task<IActionResult> RequestGoogleUnlink(RequestGoogleUnlinkDto request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return userId is null
            ? Unauthorized()
            : ToActionResult(await service.RequestGoogleUnlinkAsync(userId.Value, request, cancellationToken));
    }

    [Authorize]
    [EnableRateLimiting("otp-verify")]
    [HttpPost("google/unlink/confirm")]
    public async Task<IActionResult> ConfirmGoogleUnlink(ConfirmGoogleUnlinkDto request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return userId is null
            ? Unauthorized()
            : ToActionResult(await service.ConfirmGoogleUnlinkAsync(userId.Value, request, cancellationToken));
    }

    [Authorize]
    [EnableRateLimiting("otp-send")]
    [HttpPost("change-email/request")]
    public async Task<IActionResult> RequestEmailChange(RequestEmailChangeDto request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return userId is null
            ? Unauthorized()
            : ToActionResult(await service.RequestEmailChangeAsync(userId.Value, request, cancellationToken));
    }

    [Authorize]
    [EnableRateLimiting("otp-verify")]
    [HttpPost("change-email/verify-current")]
    public async Task<IActionResult> VerifyCurrentEmailChangeOtp(ConfirmEmailChangeOtpDto request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        return userId is null
            ? Unauthorized()
            : ToActionResult(await service.ConfirmCurrentEmailChangeOtpAsync(userId.Value, request, cancellationToken));
    }

    [Authorize]
    [EnableRateLimiting("otp-verify")]
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
    [EnableRateLimiting("password-reset")]
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
