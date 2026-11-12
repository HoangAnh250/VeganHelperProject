using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VeganHelper.DAL.Integrations.Moderation;

namespace VeganHelper.UnitTests;

public partial class DeepSeekModerationClientTests
{
    private static string ProductionPromptPath([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "../../src/VeganHelper.API/Prompts/PostModeration.v2.json"));

    [Theory]
    [InlineData("PASS")]
    [InlineData("FAIL")]
    public async Task ProductionPrompt_SendsSemanticPolicyToAiAndKeepsItsDecision(string decision)
    {
        var prompt = JsonSerializer.Deserialize<ModerationPromptDefinition>(await File.ReadAllTextAsync(ProductionPromptPath()),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        using var http = new HttpClient(new FakeHandler(async (request, ct) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var instructions = body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!;
            Assert.StartsWith(prompt.SystemInstruction, instructions);
            foreach (var policy in new[] { "spam", "repeated-character", "animal-derived", "pornographic", "profane", "Vietnamese" })
                Assert.Contains(policy, instructions, StringComparison.OrdinalIgnoreCase);
            var checks = new { title = decision, description = "PASS", ingredients = "PASS", steps = "PASS" };
            return Reply(JsonSerializer.Serialize(new { result = decision, reason = "AI policy assessment.", checks }));
        }));
        var client = new DeepSeekPostModerationClient(http, Options.Create(new ModerationAiOptions
            { Enabled = true, ApiKey = "test-key" }), prompt);
        var result = await client.ReviewAsync(Input() with { Title = "spam" }, default);
        Assert.Equal(decision == "PASS" ? "safe" : "flagged", result.Verdict);
    }
}
