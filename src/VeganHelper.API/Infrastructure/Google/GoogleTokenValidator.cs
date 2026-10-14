using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;

namespace VeganHelper.API.Infrastructure.Google;

public sealed class GoogleTokenValidator(
    IOptions<GoogleOptions> options,
    ILogger<GoogleTokenValidator> logger) : IGoogleTokenValidator
{
    private readonly GoogleOptions _options = options.Value;

    public async Task<GoogleIdentityInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(idToken))
            return null;

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_options.ClientId]
                });

            return new GoogleIdentityInfo(
                payload.Subject,
                payload.Email,
                payload.EmailVerified,
                payload.Name,
                payload.Picture);
        }
        catch (Exception exception) when (exception is InvalidJwtException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Google ID token validation failed.");
            return null;
        }
    }
}
