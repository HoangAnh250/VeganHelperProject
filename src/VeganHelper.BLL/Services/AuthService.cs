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
    IEmailSender emailSender,
    IGoogleTokenValidator googleTokenValidator,
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
        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            return ServiceResult<MessageResponseDto>.Fail("Password and confirm password must match.", 400);

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
        var emailSent = await TrySendEmailAsync(
            new EmailMessage(
                email,
                "Verify your VeganHelper account",
                $"Your VeganHelper verification code is {verificationCode}. It expires in {EmailTokenLifetimeMinutes} minutes.",
                $"<p>Your VeganHelper verification code is <strong>{verificationCode}</strong>.</p><p>This code expires in {EmailTokenLifetimeMinutes} minutes.</p>"),
            cancellationToken);
        if (!emailSent)
            return ServiceResult<MessageResponseDto>.Fail("Registration was created, but the verification email could not be sent. Please request another code.", 503);

        return ServiceResult<MessageResponseDto>.Created(new MessageResponseDto("Registration successful. Verify your email before logging in."));
    }

    public async Task<ServiceResult<MessageResponseDto>> ResendVerificationAsync(ResendVerificationRequestDto request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await repository.FindUserByEmailAsync(email, cancellationToken);
        if (user is null || user.DeletedAt is not null || user.EmailVerifiedAt is not null)
            return ServiceResult<MessageResponseDto>.Ok(new MessageResponseDto("If the account exists and is not verified, a new verification code has been sent."));

        var now = DateTime.UtcNow;
        var verificationCode = TokenSecurity.GenerateOtp();
        await repository.AddEmailVerificationTokenAsync(new EmailVerificationToken
        {
            UserId = user.Id,
            TokenHash = TokenSecurity.Hash(verificationCode),
            ExpiresAt = now.AddMinutes(EmailTokenLifetimeMinutes),
            CreatedAt = now
        }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        var emailSent = await TrySendEmailAsync(
            new EmailMessage(
                email,
                "Your new VeganHelper verification code",
                $"Your VeganHelper verification code is {verificationCode}. It expires in {EmailTokenLifetimeMinutes} minutes.",
                $"<p>Your VeganHelper verification code is <strong>{verificationCode}</strong>.</p><p>This code expires in {EmailTokenLifetimeMinutes} minutes.</p>"),
            cancellationToken);
        if (!emailSent)
            return ServiceResult<MessageResponseDto>.Fail("The verification email could not be sent. Please try again later.", 503);

        return ServiceResult<MessageResponseDto>.Ok(new MessageResponseDto("If the account exists and is not verified, a new verification code has been sent."));
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

        return await IssueTokensAsync(user, now, cancellationToken);
    }

    public async Task<ServiceResult<AuthResponseDto>> LoginWithGoogleAsync(GoogleLoginRequestDto request, CancellationToken cancellationToken)
    {
        var identity = await googleTokenValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (identity is null)
            return ServiceResult<AuthResponseDto>.Fail("The Google ID token is invalid.", 401);
        if (!identity.EmailVerified)
            return ServiceResult<AuthResponseDto>.Fail("The Google email must be verified before login.", 403);

        var email = NormalizeEmail(identity.Email);
        var providerIdentity = await repository.FindIdentityAsync(GoogleProvider, identity.Subject, cancellationToken);
        if (providerIdentity is not null)
        {
            var linkedUser = await repository.FindUserByIdAsync(providerIdentity.UserId, cancellationToken);
            if (linkedUser is null || linkedUser.DeletedAt is not null || !linkedUser.IsActive)
                return ServiceResult<AuthResponseDto>.Fail("The linked account is inactive.", 403);

            if (linkedUser.EmailVerifiedAt is null)
            {
                linkedUser.EmailVerifiedAt = DateTime.UtcNow;
                await repository.SaveChangesAsync(cancellationToken);
            }

            return await IssueTokensAsync(linkedUser, DateTime.UtcNow, cancellationToken);
        }

        if (await repository.FindUserByEmailAsync(email, cancellationToken) is not null)
            return ServiceResult<AuthResponseDto>.Fail("This email is already registered. Log in with the local account and link Google.", 409);

        var now = DateTime.UtcNow;
        var username = await CreateGoogleUsernameAsync(identity, cancellationToken);
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = null,
            EmailVerifiedAt = now,
            IsActive = true,
            FailedLoginAttempts = 0,
            CreatedAt = now,
            RoleId = 1
        };
        await repository.AddUserAsync(user, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        await userRepository.AddUserProfileAsync(new UserProfile
        {
            UserId = user.Id,
            DisplayName = string.IsNullOrWhiteSpace(identity.DisplayName) ? username : identity.DisplayName.Trim(),
            AvatarUrl = identity.PictureUrl,
            DietType = "vegan"
        }, cancellationToken);
        await repository.AddUserIdentityAsync(new UserIdentity
        {
            UserId = user.Id,
            Provider = GoogleProvider,
            ProviderSubject = identity.Subject,
            CreatedAt = now
        }, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        return await IssueTokensAsync(user, now, cancellationToken);
    }

    public async Task<ServiceResult<MessageResponseDto>> LinkGoogleAsync(long userId, GoogleLoginRequestDto request, CancellationToken cancellationToken)
    {
        var user = await repository.FindUserByIdAsync(userId, cancellationToken);
        if (user is null || user.DeletedAt is not null || !user.IsActive)
            return ServiceResult<MessageResponseDto>.Fail("User was not found.", 404);
        if (user.EmailVerifiedAt is null)
            return ServiceResult<MessageResponseDto>.Fail("Verify the local email before linking Google.", 403);

        var identity = await googleTokenValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (identity is null)
            return ServiceResult<MessageResponseDto>.Fail("The Google ID token is invalid.", 401);
        if (!identity.EmailVerified)
            return ServiceResult<MessageResponseDto>.Fail("The Google email must be verified before linking.", 403);
        if (!string.Equals(NormalizeEmail(identity.Email), user.Email, StringComparison.OrdinalIgnoreCase))
            return ServiceResult<MessageResponseDto>.Fail("The Google email must match the local account email.", 409);

        var existingIdentity = await repository.FindIdentityAsync(GoogleProvider, identity.Subject, cancellationToken);
        if (existingIdentity is not null)
        {
            return existingIdentity.UserId == userId
                ? ServiceResult<MessageResponseDto>.Fail("Google is already linked to this account.", 409)
                : ServiceResult<MessageResponseDto>.Fail("This Google account is already linked to another account.", 409);
        }

        if (await repository.FindIdentityByUserAsync(userId, GoogleProvider, cancellationToken) is not null)
            return ServiceResult<MessageResponseDto>.Fail("A Google account is already linked to this user.", 409);

        await repository.AddUserIdentityAsync(new UserIdentity
        {
            UserId = userId,
            Provider = GoogleProvider,
            ProviderSubject = identity.Subject,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ServiceResult<MessageResponseDto>.Ok(new MessageResponseDto("Google account linked successfully."));
    }

    public async Task<ServiceResult<MessageResponseDto>> UnlinkGoogleAsync(long userId, UnlinkGoogleRequestDto request, CancellationToken cancellationToken)
    {
        var user = await repository.FindUserByIdAsync(userId, cancellationToken);
        if (user is null || user.DeletedAt is not null)
            return ServiceResult<MessageResponseDto>.Fail("User was not found.", 404);
        if (user.PasswordHash is null)
            return ServiceResult<MessageResponseDto>.Fail("Set a local password before unlinking Google.", 409);

        var identity = await repository.FindIdentityByUserAsync(userId, GoogleProvider, cancellationToken);
        if (identity is null)
            return ServiceResult<MessageResponseDto>.Fail("Google is not linked to this account.", 404);
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return ServiceResult<MessageResponseDto>.Fail("The current password is incorrect.", 403);

        repository.RemoveUserIdentity(identity);
        await repository.SaveChangesAsync(cancellationToken);
        return ServiceResult<MessageResponseDto>.Ok(new MessageResponseDto("Google account unlinked successfully."));
    }

    public async Task<ServiceResult<MessageResponseDto>> SetPasswordAsync(long userId, SetPasswordRequestDto request, CancellationToken cancellationToken)
    {
        var user = await repository.FindUserByIdAsync(userId, cancellationToken);
        if (user is null || user.DeletedAt is not null)
            return ServiceResult<MessageResponseDto>.Fail("User was not found.", 404);
        if (user.PasswordHash is not null)
            return ServiceResult<MessageResponseDto>.Fail("A local password is already set.", 409);
        if (user.EmailVerifiedAt is null || !user.IsActive)
            return ServiceResult<MessageResponseDto>.Fail("Verify the account before setting a password.", 403);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        return ServiceResult<MessageResponseDto>.Ok(new MessageResponseDto("Local password set successfully."));
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

        storedToken.RevokedAt = now;
        return await IssueTokensAsync(user, now, cancellationToken);
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
            var resetLink = $"http://localhost:3000/forgot-password?token={rawToken}&email={System.Net.WebUtility.UrlEncode(email)}";
            await TrySendEmailAsync(
                new EmailMessage(
                    email,
                    "Reset your VeganHelper password",
                    $"Click the link to reset your password: {resetLink}\nIt expires in {PasswordResetLifetimeMinutes} minutes.",
                    $"<p>Click the link below to reset your password:</p><p><a href=\"{resetLink}\">{resetLink}</a></p><p>This link expires in {PasswordResetLifetimeMinutes} minutes.</p>"),
                cancellationToken);
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

    private const string GoogleProvider = "google";

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private async Task<ServiceResult<AuthResponseDto>> IssueTokensAsync(User user, DateTime now, CancellationToken cancellationToken)
    {
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

    private async Task<string> CreateGoogleUsernameAsync(GoogleIdentityInfo identity, CancellationToken cancellationToken)
    {
        var source = string.IsNullOrWhiteSpace(identity.DisplayName)
            ? identity.Email.Split('@')[0]
            : identity.DisplayName;
        var chars = source.Where(char.IsLetterOrDigit).ToArray();
        var baseName = new string(chars);
        if (baseName.Length < 3)
            baseName = "googleuser";
        if (baseName.Length > 80)
            baseName = baseName[..80];

        var candidate = baseName;
        var suffix = 1;
        while (await repository.FindUserByUsernameAsync(candidate, cancellationToken) is not null)
        {
            var suffixText = $"_{suffix++}";
            candidate = baseName[..Math.Min(baseName.Length, 100 - suffixText.Length)] + suffixText;
        }
        return candidate;
    }

    private async Task<bool> TrySendEmailAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await emailSender.SendAsync(message, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not send email to {Email}.", message.ToEmail);
            return false;
        }
    }
}
