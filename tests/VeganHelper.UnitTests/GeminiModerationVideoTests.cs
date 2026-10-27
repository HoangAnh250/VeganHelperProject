using System.Net;
using System.Text;
using System.Text.Json;

namespace VeganHelper.UnitTests;

public partial class GeminiModerationClientTests
{
    private const string VideoFileUri = "https://generativelanguage.googleapis.com/v1beta/files/test-video";

    [Theory]
    [InlineData("video/mp4", "video/mp4")]
    [InlineData("video/quicktime", "video/mov")]
    [InlineData("video/webm", "video/webm")]
    public async Task Review_UploadsFullVideoUsingPostAndUsesFileUriThenDeletesFile(string sourceMime, string providerMime)
    {
        var videoBytes = sourceMime == "video/webm" ? VideoTestData.Webm(60) : VideoTestData.Mp4(60);
        var stages = new List<string>();
        using var http = new HttpClient(new FakeHandler(async (request, ct) =>
        {
            if (request.RequestUri!.Host == "media.example.org")
            {
                stages.Add("download");
                Assert.False(request.Headers.Contains("x-goog-api-key"));
                return Image(videoBytes, sourceMime);
            }
            if (request.Method == HttpMethod.Delete)
            {
                stages.Add("delete");
                Assert.Equal(VideoFileUri, request.RequestUri.AbsoluteUri);
                return new(HttpStatusCode.NoContent);
            }
            if (request.RequestUri.AbsolutePath == "/upload/v1beta/files")
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                if (request.RequestUri.Query.Length == 0)
                {
                    stages.Add("start");
                    Assert.Equal("private-key", Assert.Single(request.Headers.GetValues("x-goog-api-key")));
                    Assert.Equal(providerMime, Assert.Single(request.Headers.GetValues("X-Goog-Upload-Header-Content-Type")));
                    Assert.Equal(videoBytes.Length.ToString(), Assert.Single(request.Headers.GetValues("X-Goog-Upload-Header-Content-Length")));
                    return StartVideoUpload();
                }
                stages.Add("upload");
                Assert.False(request.Headers.Contains("x-goog-api-key"));
                Assert.Equal("upload, finalize", Assert.Single(request.Headers.GetValues("X-Goog-Upload-Command")));
                Assert.Equal(videoBytes, await request.Content!.ReadAsByteArrayAsync(ct));
                Assert.Equal(providerMime, request.Content.Headers.ContentType!.MediaType);
                return GeminiFile("ACTIVE", providerMime);
            }
            stages.Add("review");
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var video = json.RootElement.GetProperty("input").EnumerateArray().Single(p => p.GetProperty("type").GetString() == "video");
            Assert.Equal(VideoFileUri, video.GetProperty("uri").GetString());
            Assert.Equal(providerMime, video.GetProperty("mime_type").GetString());
            Assert.Equal(1, video.GetProperty("processing").GetProperty("fps").GetInt32());
            Assert.Equal("static", video.GetProperty("processing").GetProperty("type").GetString());
            Assert.False(video.TryGetProperty("data", out _));
            Assert.False(video.TryGetProperty("media_resolution", out _)); // Rejected by the live Interactions endpoint.
            return Reply(Safe);
        }));

        var result = await Client(http).ReviewAsync(Input() with { Media = [new(1, "https://media.example.org/video", "video")] }, default);
        Assert.Equal("safe", result.Verdict);
        Assert.Equal(new[] { "download", "start", "upload", "review", "delete" }, stages);
    }

    [Theory]
    [InlineData("ACTIVE", 200, null)]
    [InlineData("FAILED", 200, "invalid_media")]
    [InlineData("ACTIVE", 429, "provider_quota")]
    [InlineData("ACTIVE", 400, "provider_error")]
    public async Task Review_PollsProcessingFileAndAlwaysCleansUpAfterResultOrProviderFailure(string fileState, int reviewStatus, string? errorCode)
    {
        var polls = 0; var deletes = 0; var reviews = 0;
        using var http = new HttpClient(new FakeHandler((request, _) =>
        {
            if (request.RequestUri!.Host == "media.example.org") return Task.FromResult(Image(VideoTestData.Mp4(10), "video/mp4"));
            if (request.Method == HttpMethod.Delete) { deletes++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)); }
            if (request.Method == HttpMethod.Get) { polls++; return Task.FromResult(GeminiFile(fileState)); }
            if (request.RequestUri.AbsolutePath == "/upload/v1beta/files")
                return Task.FromResult(request.RequestUri.Query.Length == 0 ? StartVideoUpload() : GeminiFile("PROCESSING"));
            reviews++;
            return Task.FromResult(reviewStatus == 200 ? Reply(Safe) : new HttpResponseMessage((HttpStatusCode)reviewStatus));
        }));
        var input = Input() with { Media = [new(1, "https://media.example.org/video.mp4", "video")] };
        if (errorCode is null) Assert.Equal("safe", (await Client(http).ReviewAsync(input, default)).Verdict);
        else Assert.Equal(errorCode, (await Assert.ThrowsAsync<VeganHelper.DAL.Integrations.Moderation.ModerationAiException>(() => Client(http).ReviewAsync(input, default))).Code);
        Assert.Equal(1, polls);
        Assert.Equal(1, deletes);
        Assert.Equal(fileState == "FAILED" ? 0 : 1, reviews);
    }

    [Fact]
    public async Task Review_CleansUpWhenUploadedFileImmediatelyFailsWithoutUriOrMime()
    {
        var deletes = 0;
        using var http = new HttpClient(new FakeHandler((request, _) =>
        {
            if (request.RequestUri!.Host == "media.example.org") return Task.FromResult(Image(VideoTestData.Mp4(10), "video/mp4"));
            if (request.Method == HttpMethod.Delete) { deletes++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)); }
            return Task.FromResult(request.RequestUri.Query.Length == 0 ? StartVideoUpload() : GeminiFile("FAILED"));
        }));
        var error = await Assert.ThrowsAsync<VeganHelper.DAL.Integrations.Moderation.ModerationAiException>(() => Client(http).ReviewAsync(
            Input() with { Media = [new(1, "https://media.example.org/video.mp4", "video")] }, default));
        Assert.Equal("invalid_media", error.Code);
        Assert.Equal(1, deletes);
    }

    [Theory]
    [InlineData(60.001)]
    [InlineData(90)]
    public async Task Review_RejectsActualOverlongVideoBeforeCallingGoogleEvenWithShortStoredDuration(double seconds)
    {
        using var http = new HttpClient(new FakeHandler((request, _) =>
        {
            Assert.Equal("media.example.org", request.RequestUri!.Host);
            return Task.FromResult(Image(VideoTestData.Mp4(seconds), "video/mp4"));
        }));
        var error = await Assert.ThrowsAsync<VeganHelper.DAL.Integrations.Moderation.ModerationAiException>(() => Client(http).ReviewAsync(
            Input() with { Media = [new(1, "https://media.example.org/video.mp4", "video", 30)] }, default));
        Assert.Equal("video_too_long", error.Code);
    }

    [Fact]
    public async Task Review_DoesNotSendBytesOrApiKeyToAnUntrustedUploadUrl()
    {
        using var http = new HttpClient(new FakeHandler((request, _) =>
        {
            if (request.RequestUri!.Host == "media.example.org") return Task.FromResult(Image(VideoTestData.Mp4(10), "video/mp4"));
            Assert.Equal("/upload/v1beta/files", request.RequestUri.AbsolutePath);
            Assert.Equal("generativelanguage.googleapis.com", request.RequestUri.Host);
            return Task.FromResult(StartVideoUpload("https://attacker.example/upload"));
        }));
        var error = await Assert.ThrowsAsync<VeganHelper.DAL.Integrations.Moderation.ModerationAiException>(() => Client(http).ReviewAsync(
            Input() with { Media = [new(1, "https://media.example.org/video.mp4", "video")] }, default));
        Assert.Equal("invalid_response", error.Code);
    }

    private static HttpResponseMessage StartVideoUpload(string uploadUrl = "https://generativelanguage.googleapis.com/upload/v1beta/files?upload_id=test")
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("X-Goog-Upload-URL", uploadUrl);
        return response;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Review_CancellationOrProcessingTimeoutStillDeletesUploadedFile(bool callerCancellation)
    {
        using var cancellation = new CancellationTokenSource();
        var deletes = 0;
        using var http = new HttpClient(new FakeHandler((request, ct) =>
        {
            if (request.RequestUri!.Host == "media.example.org") return Task.FromResult(Image(VideoTestData.Mp4(10), "video/mp4"));
            if (request.Method == HttpMethod.Delete)
            {
                Assert.False(ct.IsCancellationRequested);
                deletes++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }
            if (request.RequestUri.AbsolutePath == "/upload/v1beta/files")
                return Task.FromResult(request.RequestUri.Query.Length == 0 ? StartVideoUpload() : GeminiFile(callerCancellation ? "ACTIVE" : "PROCESSING"));
            cancellation.Cancel();
            throw new OperationCanceledException(ct);
        }));
        var input = Input() with { Media = [new(1, "https://media.example.org/video.mp4", "video")] };
        if (callerCancellation)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(http).ReviewAsync(input, cancellation.Token));
        else
        {
            var error = await Assert.ThrowsAsync<VeganHelper.DAL.Integrations.Moderation.ModerationAiException>(() => Client(http, timeout: 1).ReviewAsync(input, default));
            Assert.Equal("provider_timeout", error.Code);
            Assert.True(error.Retryable);
        }
        Assert.Equal(1, deletes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Review_FileUploadQuotaIsRetryableAndDoesNotReachInference(bool quotaDuringFinalize)
    {
        using var http = new HttpClient(new FakeHandler((request, _) =>
        {
            if (request.RequestUri!.Host == "media.example.org") return Task.FromResult(Image(VideoTestData.Mp4(10), "video/mp4"));
            Assert.Equal("/upload/v1beta/files", request.RequestUri.AbsolutePath);
            if (quotaDuringFinalize && request.RequestUri.Query.Length == 0) return Task.FromResult(StartVideoUpload());
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(120));
            return Task.FromResult(response);
        }));
        var error = await Assert.ThrowsAsync<VeganHelper.DAL.Integrations.Moderation.ModerationAiException>(() => Client(http).ReviewAsync(
            Input() with { Media = [new(1, "https://media.example.org/video.mp4", "video")] }, default));
        Assert.Equal("provider_quota", error.Code);
        Assert.True(error.Retryable);
        Assert.Equal(120, error.RetryAfterSeconds);
    }

    private static HttpResponseMessage GeminiFile(string state, string mime = "video/mp4")
    {
        object file = state == "FAILED" ? new { name = "files/test-video", state } :
            new { name = "files/test-video", uri = VideoFileUri, mimeType = mime, state };
        return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { file }), Encoding.UTF8, "application/json") };
    }
}
