using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using VeganHelper.BLL.Services;
using VeganHelper.DAL.Integrations;

namespace VeganHelper.API.Infrastructure.Notifications;

public sealed class WebPushOptionsValidator : IValidateOptions<WebPushSettings>
{
    public ValidateOptionsResult Validate(string? name, WebPushSettings options)
    {
        if (!options.Enabled) return ValidateOptionsResult.Success;
        try
        {
            if (!Uri.TryCreate(options.Subject, UriKind.Absolute, out var subject) || subject.Scheme is not ("mailto" or "https") ||
             !PushSubscriptionValidation.IsKey(options.PublicKey, 65, true) || !PushSubscriptionValidation.IsKey(options.PrivateKey, 32))
                return ValidateOptionsResult.Fail("WebPush requires a contact Subject and a valid VAPID key pair.");
            var publicBytes = Decode(options.PublicKey);
            using var key = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                D = Decode(options.PrivateKey),
                Q = new ECPoint { X = publicBytes[1..33], Y = publicBytes[33..65] }
            });
            var challenge = new byte[] { 1, 2, 3 };
            var signature = key.SignData(challenge, HashAlgorithmName.SHA256);
            using var verifier = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = publicBytes[1..33], Y = publicBytes[33..65] }
            });
            if (!verifier.VerifyData(challenge, signature, HashAlgorithmName.SHA256))
                return ValidateOptionsResult.Fail("VAPID public/private keys do not match.");
            return ValidateOptionsResult.Success;
        }
        catch (Exception) { return ValidateOptionsResult.Fail("Invalid WebPush configuration."); }
    }
    private static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '='));
}
