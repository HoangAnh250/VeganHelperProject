namespace VeganHelper.DAL.Integrations.Moderation;

public static class ModerationProviderErrors
{
    public static bool IsKnown(string? code)
    {
        if (code is null) return false;
        foreach (var prefix in new[] { "deepseek_http_", "twelvelabs_http_" })
            if (code.StartsWith(prefix, StringComparison.Ordinal) && code.Length == prefix.Length + 3 &&
                int.TryParse(code.AsSpan(prefix.Length), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var status) && status is >= 400 and <= 599)
                return true;
        return false;
    }
}
