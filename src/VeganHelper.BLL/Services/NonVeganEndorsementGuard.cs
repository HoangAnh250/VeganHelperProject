using System.Text.RegularExpressions;
using VeganHelper.DAL.Integrations.Moderation;

namespace VeganHelper.BLL.Services;

internal static class NonVeganEndorsementGuard
{
    private static readonly Regex AnimalFood = new(
        @"(?<![\p{L}\p{N}])(?:pork|ham|bacon|beef|chicken|poultry|lamb|mutton|meat|fish|seafood|shrimp|prawn|dairy|milk|cheese|egg|eggs|honey|thịt\s+(?:heo|lợn|bò|gà|vịt|dê|cừu)|cá|hải\s+sản|sữa|phô\s+mai|trứng|mật\s+ong)(?![\p{L}\p{N}])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PositiveEndorsement = new(
        @"\b(?:healthy|nutritious)\b|\b(?:good|beneficial)\s+for\s+(?:your\s+)?health\b|\b(?:should\s+eat|recommend\s+eating)\b|\b(?:tốt\s+cho\s+sức\s+khỏe|có\s+lợi\s+cho\s+sức\s+khỏe|bổ\s+dưỡng|lành\s+mạnh|nên\s+ăn|khuyên\s+ăn)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CriticalContext = new(
        @"\b(?:not|never|myth|false|incorrect|misleading|debunk(?:ed)?|refute(?:d)?|critic(?:ize|ise|ized|ised)|without)\b|(?:không|chẳng|chưa|sai|ngộ\s+nhận|bác\s+bỏ|phản\s+bác)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SentenceBoundary = new(
        @"(?<=[.!?;])\s+|\r?\n",
        RegexOptions.CultureInvariant);

    public static ModerationAiResult Apply(ModerationAiInput input, ModerationAiResult result)
    {
        if (result.Verdict != "safe") return result;

        foreach (var (field, text) in new[] { ("title", input.Title), ("content", input.Content ?? "") })
        {
            foreach (var sentence in SentenceBoundary.Split(text))
            {
                var candidate = sentence.Trim();
                if (candidate.Length == 0 || candidate.Contains('?') || CriticalContext.IsMatch(candidate) ||
                    !AnimalFood.IsMatch(candidate) || !PositiveEndorsement.IsMatch(candidate))
                    continue;

                var excerpt = candidate.Length <= 160 ? candidate : candidate[..157] + "...";
                const string reason = "The post endorses eating animal-derived food, which is outside this vegan community.";
                return result with
                {
                    Verdict = "flagged",
                    Summary = reason,
                    Findings = [new ModerationAiFinding("non_vegan_endorsement", field, excerpt, null, reason)]
                };
            }
        }

        return result;
    }
}