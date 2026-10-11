using System.Text.Json;
using VeganHelper.DAL.Integrations.Moderation;

namespace VeganHelper.BLL.Mapping;

public static class ModerationMapping
{
    public static List<ModerationAiFinding> FlagFindings(string? snapshot, string fallback)
    {
        if (snapshot is not null)
        {
            using var doc = JsonDocument.Parse(snapshot);
            if (doc.RootElement.TryGetProperty("findings", out var findings)) return Findings(findings.GetRawText());
        }
        return Findings(fallback);
    }
    public static double? FlagConfidence(string? snapshot, double? fallback)
    {
        if (snapshot is not null)
        {
            using var doc = JsonDocument.Parse(snapshot);
            if (doc.RootElement.TryGetProperty("confidence", out var confidence))
                return confidence.ValueKind == JsonValueKind.Number && confidence.TryGetDouble(out var value) ? value : null;
        }
        return fallback;
    }
    public static List<ModerationAiFinding> Findings(string json) => JsonSerializer.Deserialize<List<ModerationAiFinding>>(json, JsonSerializerOptions.Web) ?? [];
}
