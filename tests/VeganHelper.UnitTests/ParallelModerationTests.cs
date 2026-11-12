using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VeganHelper.DAL.Integrations.Moderation;

namespace VeganHelper.UnitTests;

public partial class DeepSeekModerationClientTests
{
    private static TaskCompletionSource<bool> Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static HttpResponseMessage PegasusReply(string decision) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        { finish_reason = "stop", data = JsonSerializer.Serialize(new { result = decision, reason = "Video policy review." }) }))
    };

    private static DeepSeekPostModerationClient ParallelClient(HttpClient http) => new(http,
        Options.Create(new ModerationAiOptions
        {
            Enabled = true, ApiKey = "private-key", TwelveLabsApiKey = "test-pegasus-key",
            AllowedMediaHosts = ["media.example.org"]
        }), Prompt());

    private static ModerationAiInput VideoInput() => Input() with
    { Media = [new(11, "https://media.example.org/video.mp4", "video", 10)] };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ParallelProviders_FirstFailReturnsWithoutWaitingAndCancelsSibling(bool deepSeekFails)
    {
        var deepSeekStarted = Signal(); var pegasusStarted = Signal(); var siblingCancelled = Signal();
        using var http = new HttpClient(new FakeHandler(async (request, ct) =>
        {
            if (request.RequestUri!.Host == "media.example.org") return Image(VideoTestData.Mp4(10), "video/mp4");
            var isDeepSeek = request.RequestUri.Host == "api.deepseek.com";
            (isDeepSeek ? deepSeekStarted : pegasusStarted).TrySetResult(true);
            await Task.WhenAll(deepSeekStarted.Task, pegasusStarted.Task).WaitAsync(ct);
            if (isDeepSeek == deepSeekFails) return isDeepSeek ? Reply(Flagged) : PegasusReply("FAIL");
            try { await Task.Delay(Timeout.Infinite, ct); }
            catch (OperationCanceledException) { siblingCancelled.TrySetResult(true); throw; }
            throw new InvalidOperationException("The sibling request should be cancelled.");
        }));
        var result = await ParallelClient(http).ReviewAsync(VideoInput(), default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("flagged", result.Verdict);
        await siblingCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ParallelProviders_SafeRequiresBothPass()
    {
        var deepSeekStarted = Signal(); var pegasusStarted = Signal(); var releaseVideo = Signal();
        using var http = new HttpClient(new FakeHandler(async (request, ct) =>
        {
            if (request.RequestUri!.Host == "media.example.org") return Image(VideoTestData.Mp4(10), "video/mp4");
            if (request.RequestUri.Host == "api.deepseek.com")
            { deepSeekStarted.TrySetResult(true); return Reply(Safe); }
            pegasusStarted.TrySetResult(true);
            await releaseVideo.Task.WaitAsync(ct);
            return PegasusReply("PASS");
        }));
        var review = ParallelClient(http).ReviewAsync(VideoInput(), default);
        await Task.WhenAll(deepSeekStarted.Task, pegasusStarted.Task).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(review.IsCompleted);
        releaseVideo.TrySetResult(true);
        var result = await review.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("safe", result.Verdict);
        Assert.Contains("pegasus1.6", result.Model);
    }

    [Theory]
    [InlineData("PASS")]
    [InlineData("FAIL")]
    public async Task ParallelProviders_MalformedDeepSeekDoesNotDiscardVideoFailOrAllowPartialPass(string videoVerdict)
    {
        var malformedRequests = 0; var deepSeekRetried = Signal();
        using var http = new HttpClient(new FakeHandler(async (request, ct) =>
        {
            if (request.RequestUri!.Host == "media.example.org") return Image(VideoTestData.Mp4(10), "video/mp4");
            if (request.RequestUri.Host == "api.deepseek.com")
            {
                if (Interlocked.Increment(ref malformedRequests) == 2) deepSeekRetried.TrySetResult(true);
                return Reply("not json");
            }
            await deepSeekRetried.Task.WaitAsync(ct);
            return PegasusReply(videoVerdict);
        }));
        if (videoVerdict == "FAIL")
            Assert.Equal("flagged", (await ParallelClient(http).ReviewAsync(VideoInput(), default).WaitAsync(TimeSpan.FromSeconds(5))).Verdict);
        else
            Assert.Equal("response_verdict_json_invalid", (await Assert.ThrowsAsync<ModerationAiException>(
                () => ParallelClient(http).ReviewAsync(VideoInput(), default).WaitAsync(TimeSpan.FromSeconds(5)))).Code);
        Assert.Equal(2, malformedRequests);
    }

    [Fact]
    public async Task ParallelProviders_CallerCancellationCancelsBothRequests()
    {
        var deepSeekStarted = Signal(); var pegasusStarted = Signal(); var bothCancelled = Signal();
        var cancelled = 0;
        using var cancellation = new CancellationTokenSource();
        using var http = new HttpClient(new FakeHandler(async (request, ct) =>
        {
            if (request.RequestUri!.Host == "media.example.org") return Image(VideoTestData.Mp4(10), "video/mp4");
            (request.RequestUri.Host == "api.deepseek.com" ? deepSeekStarted : pegasusStarted).TrySetResult(true);
            try { await Task.Delay(Timeout.Infinite, ct); }
            catch (OperationCanceledException)
            { if (Interlocked.Increment(ref cancelled) == 2) bothCancelled.TrySetResult(true); throw; }
            throw new InvalidOperationException();
        }));
        var review = ParallelClient(http).ReviewAsync(VideoInput(), cancellation.Token);
        await Task.WhenAll(deepSeekStarted.Task, pegasusStarted.Task).WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => review);
        await bothCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
    [Theory]
    [InlineData("PASS")]
    [InlineData("FAIL")]
    public async Task ParallelProviders_LongPegasusReasonDoesNotLoseTheVerdict(string decision)
    {
        var calls = 0;
        using var http = new HttpClient(new FakeHandler((request, ct) =>
        {
            if (request.RequestUri!.Host == "media.example.org") return Task.FromResult(Image(VideoTestData.Mp4(10), "video/mp4"));
            if (request.RequestUri.Host == "api.deepseek.com") return Task.FromResult(Reply(Safe));
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content = new StringContent(JsonSerializer.Serialize(new
            {finish_reason="stop",data=JsonSerializer.Serialize(new {result=decision,reason="Video policy finding. " + new string('x',500)})}))});
        }));
        var result=await ParallelClient(http).ReviewAsync(VideoInput(),default);
        Assert.Equal(decision=="FAIL" ? "flagged" : "safe",result.Verdict);
        Assert.InRange(result.Summary.Length,1,200);
        Assert.Equal(1,calls);
    }

    [Fact]
    public async Task ParallelProviders_ExplicitlyDelegatesVideoAndBoundsPegasusReasonWithSupportedPattern()
    {
        using var http = new HttpClient(new FakeHandler(async(request,ct)=>
        {
            if(request.RequestUri!.Host=="media.example.org")return Image(VideoTestData.Mp4(10),"video/mp4");
            using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            if(request.RequestUri.Host=="api.deepseek.com")
            {
                Assert.Contains("delegated",json.RootElement.GetProperty("messages")[0].GetProperty("content").GetString(),StringComparison.OrdinalIgnoreCase);
                var parts=json.RootElement.GetProperty("messages")[1].GetProperty("content");
                using var payload=JsonDocument.Parse(parts[0].GetProperty("text").GetString()!);
                Assert.Equal("text_and_images",payload.RootElement.GetProperty("reviewScope").GetString());
                Assert.Equal(11,payload.RootElement.GetProperty("delegatedVideoIds")[0].GetInt64());
                return Reply(Safe);
            }
            var reason=json.RootElement.GetProperty("response_format").GetProperty("json_schema").GetProperty("properties").GetProperty("reason");
            Assert.True(reason.TryGetProperty("pattern",out var pattern));
            Assert.Contains("200",pattern.GetString());
            Assert.False(reason.TryGetProperty("maxLength",out _));
            return PegasusReply("PASS");
        }));
        Assert.Equal("safe",(await ParallelClient(http).ReviewAsync(VideoInput(),default)).Verdict);
    }

    [Fact]
    public async Task Pegasus_ErrorEnvelopeCannotBecomeSafeEvenWithPassData()
    {
        using var http=new HttpClient(new FakeHandler((request,ct)=>
        {
            if(request.RequestUri!.Host=="media.example.org")return Task.FromResult(Image(VideoTestData.Mp4(10),"video/mp4"));
            if(request.RequestUri.Host=="api.deepseek.com")return Task.FromResult(Reply(Safe));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new
            {finish_reason="stop",error=new {code="internal_error"},data=JsonSerializer.Serialize(new{result="PASS",reason="No issue."})}))});
        }));
        var error=await Assert.ThrowsAsync<ModerationAiException>(()=>ParallelClient(http).ReviewAsync(VideoInput(),default));
        Assert.Equal("provider_blocked",error.Code);
    }

}
