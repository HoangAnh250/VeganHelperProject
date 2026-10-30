using System.Text;
using System.Text.RegularExpressions;

namespace VeganHelper.BLL.Services;

public sealed class CommentKeywordFilter : ICommentKeywordFilter
{
    private static readonly string[] BlockedTerms =
    [
        // Direct insults and hard-block abbreviations.
        "ngu", "óc chó", "oc cho", "vô học", "vo hoc", "mất dạy", "mat day", "khốn nạn", "khon nan",
        "đmm", "dmm", "đm", "dm", "đcm", "dcm", "cl", "cc",
        "đ m m", "d m m", "đ c m", "d c m", "đ m", "d m",
        // Direct English insults and attacks.
        "idiot", "moron", "dumbass", "asshole", "dickhead", "motherfucker",
        "fuck you", "go fuck yourself", "son of a bitch", "stfu", "kill yourself"
    ];

    private static readonly string[] HiddenTerms =
    [
        // Explicit profanity and direct threats are hidden for the future review queue.
        "địt", "dit", "đụ", "đéo", "deo", "lồn", "lon", "cặc", "cac", "buồi", "buoi", "đĩ",
        "địt mẹ", "dit me", "địt má", "dit ma", "đụ mẹ", "du me", "đụ má", "du ma",
        "con đĩ", "con di", "đồ con đĩ", "do con di", "thằng chó", "thang cho", "đồ chó", "do cho", "súc vật", "suc vat",
        "địt con mẹ mày", "dit con me may", "đụ con mẹ mày", "du con me may",
        "tao giết mày", "tao giet may", "giết mày đi", "giet may di", "đánh chết mày", "danh chet may",
        "chém mày", "chem may", "đốt nhà mày", "dot nha may", "tự tử đi", "tu tu di",
        // English profanity without an explicit target is held for review.
        "fuck", "fucking", "fucked", "shit", "bullshit", "bitch", "bastard", "cunt"
    ];

    private static readonly string[] NormalizedBlockedTerms = NormalizeTerms(BlockedTerms);
    private static readonly string[] NormalizedHiddenTerms = NormalizeTerms(HiddenTerms);

    public CommentModerationResult Evaluate(string content)
    {
        var normalizedContent = Normalize(content);
        if (normalizedContent.Length == 0)
        {
            return new CommentModerationResult(CommentModerationAction.Allow);
        }

        var blockedKeyword = FindWholeTerm(normalizedContent, NormalizedBlockedTerms);
        if (blockedKeyword is not null)
        {
            return new CommentModerationResult(CommentModerationAction.BlockRejected, blockedKeyword);
        }

        var hiddenKeyword = FindWholeTerm(normalizedContent, NormalizedHiddenTerms);
        return hiddenKeyword is null
            ? new CommentModerationResult(CommentModerationAction.Allow)
            : new CommentModerationResult(CommentModerationAction.HidePendingReview, hiddenKeyword);
    }

    private static string? FindWholeTerm(string content, IEnumerable<string> terms)
    {
        var searchable = $" {content} ";
        foreach (var term in terms.OrderByDescending(term => term.Length))
        {
            if (searchable.Contains($" {term} ", StringComparison.Ordinal))
            {
                return term;
            }
        }

        return null;
    }

    private static string[] NormalizeTerms(IEnumerable<string> terms) =>
        terms.Select(Normalize).Where(term => term.Length > 0).Distinct(StringComparer.Ordinal).ToArray();

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        // Fold hoi/nga tones to the same comparison key without stripping other accents.
        // Keep plain "đi" distinct from "đĩ"/"đỉ" and preserve original comment text.
        var normalized = value.ToLowerInvariant()
            .Normalize(NormalizationForm.FormD)
            .Replace('\u0309', '\u0303')
            .Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character) || char.IsWhiteSpace(character))
            {
                builder.Append(character);
            }
            else
            {
                builder.Append(' ');
            }
        }

        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }
}
