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
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await service.LoginAsync(request, cancellationToken));

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
