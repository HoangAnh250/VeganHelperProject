using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VeganHelper.BLL.Services;

public static class PostSearchIntent
{
    private static string Normalize(string value) => new(value.ToLowerInvariant().Replace('đ', 'd')
        .Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());

    public static bool IsMore(string value) => value.Contains("thêm", StringComparison.OrdinalIgnoreCase) ||
        Regex.IsMatch(Normalize(value), @"\b(more|another|them bai|them post|them mon|them vai|them nua|gui them|khac nua|bai khac)\b");
    public static bool IsRepeat(string value) => Regex.IsMatch(Normalize(value), @"\b(gui lai|xem lai|resend|repeat|send again|show again|them again|those again)\b");
    public static bool IsExplicitPostRequest(string value)
    {
        var text = Normalize(value);
        if (Regex.IsMatch(text, @"\b(database|sql|api key|system prompt|mat khau|noi bo|chinh tri)\b")) return false;
        return Regex.IsMatch(text, @"\b(posts?|bai viet)\b") &&
            Regex.IsMatch(text, @"\b(goi y|tim|gui|cho|xem|suggest|recommend|find|show|send|share|search|resend)\b");
    }
}
