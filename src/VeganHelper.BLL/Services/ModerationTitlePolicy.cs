using System.Text.RegularExpressions;
using VeganHelper.DAL.Integrations.Moderation;

namespace VeganHelper.BLL.Services;

public static class ModerationTitlePolicy
{
    private static readonly HashSet<string> Placeholders = new(StringComparer.OrdinalIgnoreCase)
        { "spam", "test", "testing", "untitled", "lorem ipsum", "asdf", "asdfgh", "qwerty" };
    private static readonly Regex RepeatedCharacter = new(@"^(\p{L}|\p{N})\1{2,}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static ModerationAiResult Apply(ModerationAiInput input, ModerationAiResult result)
    {
        if (result.Verdict != "safe") return result;
        var title = input.Title.Trim().Trim('.', '!', '?').Trim();
        if (!Placeholders.Contains(title) && !RepeatedCharacter.IsMatch(title)) return result;
        const string reason = "The title is spam or placeholder text. Please provide a meaningful title.";
        return result with
        {
            Verdict = "flagged", Summary = reason,
            Findings = [new ModerationAiFinding("spam_title", "title", title[..Math.Min(title.Length, 160)], null, reason)]
        };
    }
}
