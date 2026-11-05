using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VeganHelper.DAL.Integrations.Moderation;

namespace VeganHelper.UnitTests;

public partial class GeminiModerationClientTests
{
    [Theory]
    [InlineData(null, "PASS")]
    [InlineData(null, "FAIL")]
    [InlineData(10, "PASS")]
    public async Task Pegasus_ConfiguredKey_RoutesLegacyAndNewVideoToPegasus(int? duration, string decision)
    {
        var pegasusRequests = 0;
        var geminiRequests = 0;
        using var http = new HttpClient(new FakeHandler(async (request, ct) =>
        {
            if (request.RequestUri!.Host == "media.example.org")
                return Image(VideoTestData.Mp4(10), "video/mp4");
            if (request.RequestUri.Host == "api.twelvelabs.io")
            {
                pegasusRequests++;
                Assert.Equal("test-pegasus-key", Assert.Single(request.Headers.GetValues("x-api-key")));
                Assert.False(request.Headers.Contains("x-goog-api-key"));
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                Assert.Equal("pegasus1.6", body.RootElement.GetProperty("model_name").GetString());
                Assert.InRange(body.RootElement.GetProperty("max_tokens").GetInt32(), 512, 98304);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        finish_reason = "stop",
                        data = JsonSerializer.Serialize(new { result = decision, reason = "Video policy check completed." })
                    }))
                };
            }
            geminiRequests++;
            Assert.Equal("generativelanguage.googleapis.com", request.RequestUri.Host);
            Assert.Equal("/v1beta/interactions", request.RequestUri.AbsolutePath);
            return Reply(Safe);
        }));
        var client = new GeminiPostModerationClient(http, Options.Create(new ModerationAiOptions
        {
            Enabled = true, ApiKey = "private-key", TwelveLabsApiKey = "test-pegasus-key",
            AllowedMediaHosts = ["media.example.org"]
        }), Prompt());
        var result = await client.ReviewAsync(Input() with
        {
            Media = [new ModerationAiMedia(11, "https://media.example.org/clip.mp4", "video", duration)]
        }, default);
        Assert.Equal(1, pegasusRequests);
        Assert.Equal(decision == "PASS" ? 1 : 0, geminiRequests);
        Assert.Equal(decision == "PASS" ? "safe" : "flagged", result.Verdict);
        Assert.Contains("pegasus1.6", result.Model);
    }
}
