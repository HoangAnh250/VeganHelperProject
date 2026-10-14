using System.Security.Cryptography;
using System.Text;

namespace VeganHelper.BLL.Services;

internal static class TokenSecurity
{
    public static string GenerateOpaqueToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }

    public static string GenerateOtp() => RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");

    public static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
