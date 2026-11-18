using System.Globalization;
using System.Text;

namespace VeganHelper.DAL.Repositories;

public static class PostSearchTerms
{
    // Query topics are alternatives, never additional required ingredients.
    private static readonly string[][] Topics =
    [
        ["bánh ngọt", "banh ngot", "dessert", "cake", "brownie", "cookie", "pudding", "cheesecake"],
        ["salad", "gỏi", "goi", "nộm", "nom"],
        ["soup", "súp", "sup", "canh"],
        ["noodle", "bún", "bun", "phở", "pho", "ramen"]
    ];

    public static string[] Expand(IReadOnlyList<string> values)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values.Take(5).Where(v => !string.IsNullOrWhiteSpace(v)))
        {
            var term = value.Trim();
            if (term.Length > 60 || term.Any(char.IsControl)) continue;
            var normalized = Normalize(term);
            if (normalized is "vegan" or "vegetarian" or "an chay" or "thuan chay" or "mon chay"
                or "chay" or "post" or "posts" or "bai viet" or "recipe" or "recipes") continue;
            terms.Add(term);
            // Expand a topic label, not an entire specific dish into every dessert.
            foreach (var group in Topics.Where(g => g.Take(4).Any(x => Normalize(x) == normalized)))
                foreach (var alternative in group) terms.Add(alternative);
        }
        return terms.Take(20).ToArray();
    }

    private static string Normalize(string value) => new(value.ToLowerInvariant().Replace('đ', 'd')
        .Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
}
