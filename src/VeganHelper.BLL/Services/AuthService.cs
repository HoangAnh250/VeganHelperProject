using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class AuthService(
    IAuthRepository repository,
    IUserRepository userRepository,
    IJwtTokenService jwtTokenService,
    IOptions<JwtOptions> jwtOptions,
    ILogger<AuthService> logger) : IAuthService
{
    private const int EmailTokenLifetimeMinutes = 15;
    private const int PasswordResetLifetimeMinutes = 15;
    private const int MaxFailedLoginAttempts = 5;
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<ServiceResult<MessageResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var username = request.Username.Trim();
        if (await repository.FindUserByEmailAsync(email, cancellationToken) is not null)
            return ServiceResult<MessageResponseDto>.Fail("Email is already registered.", 409);
        if (await repository.FindUserByUsernameAsync(username, cancellationToken) is not null)
            return ServiceResult<MessageResponseDto>.Fail("Username is already registered.", 409);

        var now = DateTime.UtcNow;
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = false,
            FailedLoginAttempts = 0,
            CreatedAt = now,
            RoleId = 1
        };
        await repository.AddUserAsync(user, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        await userRepository.AddUserProfileAsync(new UserProfile
        {
            UserId = user.Id,
            DisplayName = username,
            DietType = "vegan"
        }, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        var verificationCode = TokenSecurity.GenerateOtp();
        await repository.AddEmailVerificationTokenAsync(new EmailVerificationToken
        {
            UserId = user.Id,
            TokenHash = TokenSecurity.Hash(verificationCode),
            ExpiresAt = now.AddMinutes(EmailTokenLifetimeMinutes),
            CreatedAt = now
        }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Development email verification OTP for {Email}: {Otp}", email, verificationCode);

        return ServiceResult<MessageResponseDto>.Created(new MessageResponseDto("Registration successful. Verify your email before logging in."));
    }

    public async Task<ServiceResult<MessageResponseDto>> VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await repository.FindUserByEmailAsync(email, cancellationToken);
        if (user is null)
            return ServiceResult<MessageResponseDto>.Fail("Invalid verification request.", 400);
        if (user.EmailVerifiedAt is not null)
            return ServiceResult<MessageResponseDto>.Fail("Email is already verified.", 400);

        var token = await repository.FindEmailVerificationTokenAsync(TokenSecurity.Hash(request.Otp), cancellationToken);
        if (token is null || token.UserId != user.Id || token.ConsumedAt is not null || token.ExpiresAt <= DateTime.UtcNow)
            return ServiceResult<MessageResponseDto>.Fail("The verification code is invalid or expired.", 400);

        var now = DateTime.UtcNow;
        token.ConsumedAt = now;
        user.EmailVerifiedAt = now;
        user.IsActive = true;
        user.UpdatedAt = now;
        await repository.SaveChangesAsync(cancellationToken);
        return ServiceResult<MessageResponseDto>.Ok(new MessageResponseDto("Email verified successfully."));
    }

    public async Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await repository.FindUserByEmailAsync(email, cancellationToken);
        if (user is null || user.PasswordHash is null)
            return ServiceResult<AuthResponseDto>.Fail("Invalid email or password.", 401);

        var now = DateTime.UtcNow;
        if (user.LockedUntil is not null && user.LockedUntil > now)
            return ServiceResult<AuthResponseDto>.Fail("Account is temporarily locked. Try again later.", 401);
        if (user.LockedUntil is not null && user.LockedUntil <= now)
        {
            user.LockedUntil = null;
            user.FailedLoginAttempts = 0;
        }
        if (user.DeletedAt is not null || !user.IsActive)
            return ServiceResult<AuthResponseDto>.Fail("Account is inactive.", 403);
        if (user.EmailVerifiedAt is null)
            return ServiceResult<AuthResponseDto>.Fail("Email must be verified before login.", 403);

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
                user.LockedUntil = now.AddMinutes(15);
            await repository.SaveChangesAsync(cancellationToken);
            return ServiceResult<AuthResponseDto>.Fail("Invalid email or password.", 401);
        }

        var roleName = await repository.FindRoleNameAsync(user.RoleId, cancellationToken) ?? "member";
        var access = jwtTokenService.CreateAccessToken(user, roleName, now);
        var rawRefreshToken = TokenSecurity.GenerateOpaqueToken();
        var refreshExpiresAt = now.AddDays(_jwtOptions.RefreshTokenDays);
        await repository.AddRefreshTokenAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = TokenSecurity.Hash(rawRefreshToken),
            ExpiresAt = refreshExpiresAt,
            CreatedAt = now
        }, cancellationToken);
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        user.UpdatedAt = now;
        await repository.SaveChangesAsync(cancellationToken);
        return ServiceResult<AuthResponseDto>.Ok(new AuthResponseDto(access.Token, rawRefreshToken, access.ExpiresAt, refreshExpiresAt));
    }

    public async Task<ServiceResult<AuthResponseDto>> RefreshAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var storedToken = await repository.FindRefreshTokenAsync(TokenSecurity.Hash(request.RefreshToken), cancellationToken);
        if (storedToken is null || storedToken.RevokedAt is not null || storedToken.ExpiresAt <= now)
            return ServiceResult<AuthResponseDto>.Fail("Refresh token is invalid or expired.", 401);
        var user = await repository.FindUserByIdAsync(storedToken.UserId, cancellationToken);
        if (user is null || !user.IsActive || user.DeletedAt is not null)
            return ServiceResult<AuthResponseDto>.Fail("Account is inactive.", 401);

        var roleName = await repository.FindRoleNameAsync(user.RoleId, cancellationToken) ?? "member";
        storedToken.RevokedAt = now;
        var access = jwtTokenService.CreateAccessToken(user, roleName, now);
        var rawRefreshToken = TokenSecurity.GenerateOpaqueToken();
        var refreshExpiresAt = now.AddDays(_jwtOptions.RefreshTokenDays);
        await repository.AddRefreshTokenAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = TokenSecurity.Hash(rawRefreshToken),
            ExpiresAt = refreshExpiresAt,
            CreatedAt = now
        }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ServiceResult<AuthResponseDto>.Ok(new AuthResponseDto(access.Token, rawRefreshToken, access.ExpiresAt, refreshExpiresAt));
    }

    public async Task<ServiceResult<MessageResponseDto>> LogoutAsync(long userId, LogoutRequestDto request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            await repository.RevokeRefreshTokensAsync(userId, cancellationToken);
        }
        else
        {
            var token = await repository.FindRefreshTokenAsync(TokenSecurity.Hash(request.RefreshToken), cancellationToken);
            if (token is not null && token.UserId == userId && token.RevokedAt is null)
                token.RevokedAt = now;
        }
        await repository.SaveChangesAsync(cancellationToken);
        return ServiceResult<MessageResponseDto>.Ok(new MessageResponseDto("Logged out successfully."));
    }

    public async Task<ServiceResult<MessageResponseDto>> ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await repository.FindUserByEmailAsync(email, cancellationToken);
        if (user is not null && user.DeletedAt is null)
        {
            var now = DateTime.UtcNow;
            var rawToken = TokenSecurity.GenerateOpaqueToken();
            await repository.AddPasswordResetTokenAsync(new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = TokenSecurity.Hash(rawToken),
                ExpiresAt = now.AddMinutes(PasswordResetLifetimeMinutes),
                CreatedAt = now
            }, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Development password reset token for {Email}: {Token}", email, rawToken);
        }
        return ServiceResult<MessageResponseDto>.Ok(new MessageResponseDto("If the email exists, a password reset link has been sent."));
    }

    public async Task<ServiceResult<MessageResponseDto>> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken)
    {
        var user = await repository.FindUserByEmailAsync(NormalizeEmail(request.Email), cancellationToken);
        var token = await repository.FindPasswordResetTokenAsync(TokenSecurity.Hash(request.Token), cancellationToken);
        var now = DateTime.UtcNow;
        if (user is null || token is null || token.UserId != user.Id || token.UsedAt is not null || token.ExpiresAt <= now)
            return ServiceResult<MessageResponseDto>.Fail("The reset token is invalid or expired.", 400);

        token.UsedAt = now;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.UpdatedAt = now;
        await repository.RevokeRefreshTokensAsync(user.Id, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ServiceResult<MessageResponseDto>.Ok(new MessageResponseDto("Password reset successfully."));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
