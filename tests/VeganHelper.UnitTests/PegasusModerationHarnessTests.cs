using System.Net;
using System.Text.Json;
using VeganHelper.DAL.Integrations.Moderation;

namespace VeganHelper.UnitTests;

public partial class DeepSeekModerationClientTests
{
    [Theory]
    [InlineData("{\"result\":\"safe\",\"reason\":\"Fine\"}", "response_verdict_invalid")]
    [InlineData("{\"result\":true,\"reason\":\"Fine\"}", "response_field_type")]
    [InlineData("{\"result\":\"PASS\"}", "response_fields_missing")]
    [InlineData("{\"result\":\"PASS\",\"reason\":null}", "response_field_type")]
    [InlineData("{\"result\":\"PASS\",\"reason\":\"  \"}", "response_field_empty")]
    [InlineData("{\"result\":\"PASS\",\"reason\":\"Fine\",\"extra\":true}", "response_fields_extra")]
    [InlineData("{\"result\":\"FAIL\",\"result\":\"PASS\",\"reason\":\"Fine\"}", "response_fields_duplicate")]
    [InlineData("Here is the result: {\"result\":\"PASS\",\"reason\":\"Fine\"}", "response_verdict_json_invalid")]
    [InlineData("[]", "response_envelope_invalid")]
    public async Task Pegasus_InvalidVerdictNeverBecomesSafe(string verdict, string expectedCode)
    {
        var error = await RejectPegasusEnvelopeAsync(JsonSerializer.Serialize(new { finish_reason = "stop", data = verdict }));
        Assert.Equal(expectedCode, error.Code);
    }

    [Theory]
    [InlineData("{}", "response_envelope_invalid")]
    [InlineData("{\"finish_reason\":\"length\",\"data\":\"{}\"}", "response_incomplete")]
    [InlineData("{\"finish_reason\":\"stop\",\"data\":{}}", "response_output_missing")]
    [InlineData("{\"finish_reason\":\"stop\",\"data\":\"\"}", "response_output_missing")]
    [InlineData("not json", "response_json_invalid")]
    public async Task Pegasus_InvalidEnvelopeNeverBecomesSafe(string envelope, string expectedCode)
    {
        var error = await RejectPegasusEnvelopeAsync(envelope);
        Assert.Equal(expectedCode, error.Code);
    }

    [Fact]
    public async Task Pegasus_OversizedEnvelopeIsRejected()
    {
        var error = await RejectPegasusEnvelopeAsync(new string('x', 256 * 1024 + 1));
        Assert.Equal("response_too_large", error.Code);
    }

    private static async Task<ModerationAiException> RejectPegasusEnvelopeAsync(string envelope)
    {
        using var http = new HttpClient(new FakeHandler((request, ct) =>
        {
            if (request.RequestUri!.Host == "media.example.org")
                return Task.FromResult(Image(VideoTestData.Mp4(10), "video/mp4"));
            if (request.RequestUri.Host == "api.deepseek.com")
                return Task.FromResult(Reply(Safe));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(envelope) });
        }));
        return await Assert.ThrowsAsync<ModerationAiException>(() => ParallelClient(http).ReviewAsync(VideoInput(), default));
    }
}
