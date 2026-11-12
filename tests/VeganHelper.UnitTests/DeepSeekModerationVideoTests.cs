using VeganHelper.DAL.Integrations.Moderation;

namespace VeganHelper.UnitTests;

public partial class DeepSeekModerationClientTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VideoWithoutPegasusKey_CannotBecomeSafeButTextCanStillFlag(bool textFails)
    {
        using var http = new HttpClient(new FakeHandler((request, _) =>
        {
            Assert.Equal("api.deepseek.com", request.RequestUri!.Host);
            return Task.FromResult(Reply(textFails ? Flagged : Safe));
        }));
        if (textFails)
        {
            var result = await Client(http).ReviewAsync(VideoInput(), default);
            Assert.Equal("flagged", result.Verdict);
        }
        else
        {
            var error = await Assert.ThrowsAsync<ModerationAiException>(() => Client(http).ReviewAsync(VideoInput(), default));
            Assert.Equal("video_provider_unavailable", error.Code);
        }
    }

    [Theory]
    [InlineData(60.001)]
    [InlineData(90)]
    public async Task VideoLongerThanLimit_DoesNotReachPegasusEvenWithShortStoredDuration(double seconds)
    {
        using var http = new HttpClient(new FakeHandler((request, _) =>
        {
            if (request.RequestUri!.Host == "api.deepseek.com")
                return Task.FromResult(Reply(Safe));

            Assert.Equal("media.example.org", request.RequestUri.Host);
            return Task.FromResult(Image(VideoTestData.Mp4(seconds), "video/mp4"));
        }));
        var error = await Assert.ThrowsAsync<ModerationAiException>(() => ParallelClient(http).ReviewAsync(VideoInput(), default));
        Assert.Equal("video_too_long", error.Code);
    }
}
