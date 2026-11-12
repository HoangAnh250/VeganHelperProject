using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VeganHelper.DAL.Integrations.Moderation;

namespace VeganHelper.UnitTests;

public partial class DeepSeekModerationClientTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pegasus_InvalidHttp200_RetriesAtMostOnceAndRequiresValidVerdict(bool alwaysMalformed)
    {
        var requests = 0;
        var logger = new DiagnosticLogger();
        using var http = new HttpClient(new FakeHandler(async (request, ct) =>
        {
            if (request.RequestUri!.Host == "media.example.org")
                return Image(VideoTestData.Mp4(10), "video/mp4");

            if (request.RequestUri.Host == "api.deepseek.com") return Reply(Safe);
            Assert.Equal("api.twelvelabs.io", request.RequestUri.Host);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var format = body.RootElement.GetProperty("response_format");
            Assert.Equal("json_schema", format.GetProperty("type").GetString());
            Assert.Equal(2, format.GetProperty("json_schema").GetProperty("required").GetArrayLength());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    finish_reason = "stop",
                    data = ++requests == 1 || alwaysMalformed ? "sensitive malformed output" :
                        string.Join('\n', "```json", """{"result":"FAIL","reason":"Off-topic video."}""", "```")
                }))
            };
        }));
        var client = new DeepSeekPostModerationClient(http, Options.Create(new ModerationAiOptions
        {
            Enabled = true, ApiKey = "private-key", TwelveLabsApiKey = "test-pegasus-key",
            AllowedMediaHosts = ["media.example.org"]
        }), Prompt(), logger);
        var input = Input() with
        {
            Media = [new ModerationAiMedia(11, "https://media.example.org/clip.mp4", "video", 10)]
        };
        if (alwaysMalformed)
        {
            var error = await Assert.ThrowsAsync<ModerationAiException>(() => client.ReviewAsync(input, default));
            Assert.Equal("response_verdict_json_invalid", error.Code);
            Assert.False(error.Retryable);
        }
        else
        {
            var result = await client.ReviewAsync(input, default);
            Assert.Equal("flagged", result.Verdict);
        }

        Assert.Equal(2, requests);
        Assert.Contains(logger.Messages, message => message.Contains("TwelveLabs") && message.Contains("response_verdict_json_invalid"));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("sensitive malformed output"));
    }

    [Fact]
    public async Task Review_DeepSeekInvalidHttp200_RetriesOnceAndAcceptsCorrectedVerdict()
    {
        var requests = 0;
        using var http = new HttpClient(new FakeHandler((_, _) =>
            Task.FromResult(Reply(++requests == 1 ? """{"result":"PASS"}""" : Safe))));
        var result = await Client(http).ReviewAsync(Input(), default);

        Assert.Equal(2, requests);
        Assert.Equal("safe", result.Verdict);
    }

    [Theory]
    [InlineData(null, "PASS")]
    [InlineData(null, "FAIL")]
    [InlineData(10, "PASS")]
    public async Task Pegasus_ConfiguredKey_RoutesLegacyAndNewVideoToPegasus(int? duration, string decision)
    {
        var pegasusRequests = 0;
        var deepSeekRequests = 0;
        using var http = new HttpClient(new FakeHandler(async (request, ct) =>
        {
            if (request.RequestUri!.Host == "media.example.org")
                return Image(VideoTestData.Mp4(10), "video/mp4");
            if (request.RequestUri.Host == "api.twelvelabs.io")
            {
                pegasusRequests++;
                Assert.Equal("test-pegasus-key", Assert.Single(request.Headers.GetValues("x-api-key")));
                Assert.Null(request.Headers.Authorization);
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
            deepSeekRequests++;
            Assert.Equal("api.deepseek.com", request.RequestUri.Host);
            Assert.Equal("/chat/completions", request.RequestUri.AbsolutePath);
            return Reply(Safe);
        }));
        var client = new DeepSeekPostModerationClient(http, Options.Create(new ModerationAiOptions
        {
            Enabled = true, ApiKey = "private-key", TwelveLabsApiKey = "test-pegasus-key",
            AllowedMediaHosts = ["media.example.org"]
        }), Prompt());
        var result = await client.ReviewAsync(Input() with
        {
            Media = [new ModerationAiMedia(11, "https://media.example.org/clip.mp4", "video", duration)]
        }, default);
        Assert.Equal(1, pegasusRequests);
        Assert.Equal(1, deepSeekRequests);
        Assert.Equal(decision == "PASS" ? "safe" : "flagged", result.Verdict);
        Assert.Contains("pegasus1.6", result.Model);
    }
}
