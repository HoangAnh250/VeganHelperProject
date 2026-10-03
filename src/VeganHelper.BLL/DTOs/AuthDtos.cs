using System.ComponentModel.DataAnnotations;

namespace VeganHelper.BLL.DTOs;

public sealed class RegisterRequestDto
{
    [Required, StringLength(100, MinimumLength = 3)]
    public string Username { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(255)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 3)]
    public string FullName { get; init; } = string.Empty;

    [Required, MinLength(8), StringLength(128)]
    public string Password { get; init; } = string.Empty;

    [Required, Compare(nameof(Password), ErrorMessage = "Password and confirm password must match.")]
    public string ConfirmPassword { get; init; } = string.Empty;
}

public sealed class VerifyEmailRequestDto
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required, RegularExpression("^[0-9]{6}$")]
    public string Otp { get; init; } = string.Empty;
}

public sealed class ResendVerificationRequestDto
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;
}

public sealed class LoginRequestDto
{
    [Required, StringLength(255)]
    public string Identifier { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed class GoogleLoginRequestDto
{
    [Required]
    public string IdToken { get; init; } = string.Empty;
}

public sealed class SetPasswordRequestDto
{
    [Required, MinLength(8), StringLength(128)]
    public string NewPassword { get; init; } = string.Empty;

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Password and confirm password must match.")]
    public string ConfirmPassword { get; init; } = string.Empty;
}

public sealed class ChangePasswordRequestDto
{
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required, MinLength(8), StringLength(128)]
    public string NewPassword { get; init; } = string.Empty;

    [Required, Compare(nameof(NewPassword), ErrorMessage = "Password and confirm password must match.")]
    public string ConfirmPassword { get; init; } = string.Empty;
}

public sealed class RequestGoogleUnlinkDto
{
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;
}

public sealed class ConfirmGoogleUnlinkDto
{
    [Required, RegularExpression("^[0-9]{6}$")]
    public string Otp { get; init; } = string.Empty;
}

public sealed class RequestEmailChangeDto
{
    [Required, EmailAddress, StringLength(255)]
    public string NewEmail { get; init; } = string.Empty;

    [Required]
    public string CurrentPassword { get; init; } = string.Empty;
}

public sealed class ConfirmEmailChangeOtpDto
{
    [Required, RegularExpression("^[0-9]{6}$")]
    public string Otp { get; init; } = string.Empty;
}

public sealed class RefreshTokenRequestDto
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed class LogoutRequestDto
{
    public string? RefreshToken { get; init; }
}

public sealed class ForgotPasswordRequestDto
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;
}

public sealed class ResetPasswordRequestDto
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Token { get; init; } = string.Empty;

    [Required, MinLength(8), StringLength(128)]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed record MessageResponseDto(string Message);

public sealed record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt);
