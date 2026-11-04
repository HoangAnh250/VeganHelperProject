using System.Text.Json;
using Microsoft.Extensions.Options;
using VeganHelper.DAL.Integrations.Moderation;
using VeganHelper.BLL.Services;

namespace VeganHelper.UnitTests;

public partial class GeminiModerationClientTests
{
    private static ModerationPromptDefinition CoveragePrompt() => new()
    {
        Version = "post-moderation.v2.6", SystemInstruction = "Review each target independently.",
        ResponseSchema = JsonSerializer.SerializeToElement(new
        {
            type = "object", additionalProperties = false,
            required = new[] { "result", "reason", "checks" },
            properties = new { result = new { type = "string" }, reason = new { type = "string" }, checks = new { type = "object" } }
        })
    };

    [Theory]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("media_11")]
    public async Task Coverage_OneFailedTargetOverridesOverallPass(string failedTarget)
    {
        using var http = new HttpClient(new FakeHandler(async (request, ct) =>
        {
            if (request.Method == HttpMethod.Get) return Image();
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var schema = body.RootElement.GetProperty("response_format").GetProperty("schema");
            var required = schema.GetProperty("properties").GetProperty("checks").GetProperty("required")
                .EnumerateArray().Select(x => x.GetString()!).ToArray();
            Assert.Equal(new[] { "title", "description", "ingredients", "steps", "media_11" }, required);
            var checks = required.ToDictionary(key => key, key => key == failedTarget ? "FAIL" : "PASS");
            return Reply(JsonSerializer.Serialize(new { result = "PASS", reason = "Compliant.", checks }));
        }));
        var client = new GeminiPostModerationClient(http, Options.Create(new ModerationAiOptions
            { Enabled = true, ApiKey = "private-key", AllowedMediaHosts = ["media.example.org"] }), CoveragePrompt());
        var result = await client.ReviewAsync(Input() with { Media = [Media(11)] }, default);
        Assert.Equal("flagged", result.Verdict);
        Assert.Equal(failedTarget, Assert.Single(result.Findings).Field);
        Assert.DoesNotContain("Compliant.", result.Summary);
    }

    [Theory]
    [InlineData("{\"title\":\"PASS\"}")]
    [InlineData("{\"title\":\"PASS\",\"description\":\"PASS\",\"ingredients\":\"PASS\",\"steps\":\"PASS\",\"extra\":\"PASS\"}")]
    [InlineData("{\"title\":\"PASS\",\"title\":\"PASS\",\"description\":\"PASS\",\"ingredients\":\"PASS\",\"steps\":\"PASS\"}")]
    [InlineData("{\"title\":\"UNKNOWN\",\"description\":\"PASS\",\"ingredients\":\"PASS\",\"steps\":\"PASS\"}")]
    public async Task Coverage_RejectsIncompleteUnknownOrDuplicateTargets(string checks)
    {
        var output = "{\"result\":\"PASS\",\"reason\":\"Compliant.\",\"checks\":" + checks + "}";
        using var http = new HttpClient(new FakeHandler((_, _) => Task.FromResult(Reply(output))));
        var client = new GeminiPostModerationClient(http, Options.Create(new ModerationAiOptions
            { Enabled = true, ApiKey = "private-key" }), CoveragePrompt());
        var error = await Assert.ThrowsAsync<ModerationAiException>(() => client.ReviewAsync(Input(), default));
        Assert.Equal("invalid_response", error.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Coverage_AllMediaMustBeAccountedFor(bool omitSecondImage)
    {
        using var http = new HttpClient(new FakeHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Get) return Task.FromResult(Image());
            var checks = new Dictionary<string, string>
            {
                ["title"] = "PASS", ["description"] = "PASS", ["ingredients"] = "PASS",
                ["steps"] = "PASS", ["media_11"] = "PASS"
            };
            if (!omitSecondImage) checks["media_12"] = "PASS";
            return Task.FromResult(Reply(JsonSerializer.Serialize(new { result = "PASS", reason = "Compliant.", checks })));
        }));
        var client = new GeminiPostModerationClient(http, Options.Create(new ModerationAiOptions
            { Enabled = true, ApiKey = "private-key", AllowedMediaHosts = ["media.example.org"] }), CoveragePrompt());
        var input = Input() with { Media = [Media(11), Media(12)] };
        if (omitSecondImage)
            Assert.Equal("invalid_response", (await Assert.ThrowsAsync<ModerationAiException>(() => client.ReviewAsync(input, default))).Code);
        else
        {
            var result = await client.ReviewAsync(input, default);
            Assert.Equal("safe", result.Verdict);
            Assert.Empty(result.Findings);
        }
    }

    [Theory]
    [InlineData("spam", "flagged")]
    [InlineData("aaa", "flagged")]
    [InlineData("QWERTY!", "flagged")]
    [InlineData("Spam prevention in our vegan community", "safe")]
    [InlineData("Test kitchen: tofu noodles", "safe")]
    [InlineData("Bún chay", "safe")]
    public void TitlePolicy_DoesNotLetHealthyMediaExcusePlaceholderTitle(string title, string verdict)
    {
        var result = new ModerationAiResult("safe", null, "Compliant.", [], "gemini-test",
            "post-moderation.v2.6", 1, 1, 1);
        var reviewed = ModerationTitlePolicy.Apply(Input() with { Title = title }, result);
        Assert.Equal(verdict, reviewed.Verdict);
    }
}
