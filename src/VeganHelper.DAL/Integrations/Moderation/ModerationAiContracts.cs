using System.Text.Json;

namespace VeganHelper.DAL.Integrations.Moderation;

public record ModerationAiMedia(long Id, string Url, string MediaType);

public record ModerationAiInput(long PostId, int Revision, string Title, string? Content,
    IReadOnlyList<ModerationAiMedia> Media, IReadOnlyList<string> Ingredients, IReadOnlyList<string> Steps);

public record ModerationAiFinding(string Code, string Field, string Excerpt, long? MediaId, string Reason);

public record ModerationAiResult(string Verdict, double Confidence, string Summary,
    IReadOnlyList<ModerationAiFinding> Findings, string Model, string PromptVersion,
    int? InputTokens, int? OutputTokens, int LatencyMs);

public interface IPostModerationAiClient
{
    Task<ModerationAiResult> ReviewAsync(ModerationAiInput input, CancellationToken ct);
}

public sealed class ModerationAiException(string code, bool retryable) : Exception(code)
{
    public string Code { get; } = code;
    public bool Retryable { get; } = retryable;
}

public sealed class ModerationAiOptions
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gemini-3.8-flash";
    public double MinAutoPublishConfidence { get; set; } = .9;
    public string[] AllowedMediaHosts { get; set; } = [];
    public int TimeoutSeconds { get; set; } = 60;
}

public sealed class ModerationPromptDefinition
{
    public string Version { get; init; } = "";
    public string SystemInstruction { get; init; } = "";
    public JsonElement ResponseSchema { get; init; }
}
