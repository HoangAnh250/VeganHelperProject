using System.Text.Json;
using VeganHelper.DAL.Integrations.Moderation;

namespace VeganHelper.BLL.Mapping;

public static class ModerationMapping
{
    public static List<ModerationAiFinding> Findings(string json) => JsonSerializer.Deserialize<List<ModerationAiFinding>>(json, JsonSerializerOptions.Web) ?? [];
}
