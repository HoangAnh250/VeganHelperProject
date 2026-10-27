using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using VeganHelper.DAL.Media;

namespace VeganHelper.DAL.Integrations.Moderation;

public sealed class GeminiPostModerationClient(HttpClient httpClient, IOptions<ModerationAiOptions> options,
    ModerationPromptDefinition prompt, ILogger<GeminiPostModerationClient>? logger = null) : IPostModerationAiClient
{
    private const int MaxImageBytes = 5 * 1024 * 1024;
    private const int MaxTotalImageBytes = 20 * 1024 * 1024;
    private const long MaxVideoBytes = 200L * 1024 * 1024;
    private const double MaxVideoDurationSeconds = 60;
    // The provider's inline-data limit includes the base64 payload, instructions and text.
    private const int MaxRequestBytes = 20_000_000;
    private const int MaxResponseBytes = 256 * 1024;
    private const int MaxTextChars = 100_000;
    private static readonly Uri Endpoint = new("https://generativelanguage.googleapis.com/v1beta/interactions");
    private static readonly Uri FilesUploadEndpoint = new("https://generativelanguage.googleapis.com/upload/v1beta/files");
    private static readonly Uri FilesApiEndpoint = new("https://generativelanguage.googleapis.com/v1beta/");
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;
    private static readonly Regex ModelName = new("\\Agemini-[a-z0-9][a-z0-9.-]{0,79}\\z", RegexOptions.CultureInvariant);

    public async Task<ModerationAiResult> ReviewAsync(ModerationAiInput input, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var settings = options.Value;
        if (!settings.Enabled)
            throw new ModerationAiException("ai_disabled", false);
        ValidateConfiguration(settings);
        var mediaUris = ValidateInput(input, settings);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        var token = timeout.Token;
        var stopwatch = Stopwatch.StartNew();
        var uploadedGeminiFiles = new List<string>();
        var temporaryFiles = new List<string>();

        try
        {
            // User text remains a JSON payload in the user input, separate from system instructions.
            var parts = new List<object>
            {
                new
                {
                    type = "text",
                    text = JsonSerializer.Serialize(new
                    {
                        input.PostId, input.Revision, input.Title, input.Content, input.Ingredients, input.Steps,
                        media = input.Media.Select(m => new { m.Id, m.MediaType, m.DurationSeconds }).ToArray()
                    }, JsonOptions)
                }
            };
            var totalBytes = 0;
            for (var index = 0; index < input.Media.Count; index++)
            {
                // A label immediately before each asset makes its database identifier unambiguous.
                parts.Add(new { type = "text", text = JsonSerializer.Serialize(new { mediaId = input.Media[index].Id }) });
                if (IsImage(input.Media[index].MediaType))
                {
                    var image = await DownloadImageAsync(mediaUris[index], token);
                    totalBytes = checked(totalBytes + image.Bytes.Length);
                    if (totalBytes > MaxTotalImageBytes)
                        throw new ModerationAiException("input_too_large", false);
                    parts.Add(new { type = "image", data = Convert.ToBase64String(image.Bytes), mime_type = image.MimeType });
                }
                else
                {
                    var video = await DownloadVideoAsync(mediaUris[index], input.Media[index].MediaType, token);
                    temporaryFiles.Add(video.Path);
                    var uploaded = await UploadVideoAsync(video.Path, video.MimeType, input.PostId, input.Media[index].Id,
                        settings, uploadedGeminiFiles, token);
                    parts.Add(new
                    {
                        type = "video", uri = uploaded.Uri, mime_type = uploaded.MimeType,
                        processing = new { type = "static", fps = 1 }
                    });
                }
            }

            var body = JsonSerializer.SerializeToUtf8Bytes(new
            {
                model = settings.Model,
                store = false,
                system_instruction = prompt.SystemInstruction,
                input = parts,
                response_format = new { type = "text", mime_type = "application/json", schema = prompt.ResponseSchema },
                // This cap includes thinking tokens; reserve room for analysis, constrain final JSON separately.
                generation_config = new { max_output_tokens = 2048, thinking_level = "low", thinking_summaries = "none" }
            }, JsonOptions);
            if (body.Length > MaxRequestBytes)
                throw new ModerationAiException("input_too_large", false);

            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = new ByteArrayContent(body) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Headers.Add("x-goog-api-key", settings.ApiKey);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode)
            {
                await LogProviderFailureAsync(response, settings.Model, token, ct);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    throw new ModerationAiException("provider_quota", true,
                        response.Headers.RetryAfter?.Delta?.TotalSeconds ??
                        (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow)?.TotalSeconds);
                throw new ModerationAiException("provider_error", IsTransient(response.StatusCode));
            }
            var bytes = await ReadBoundedAsync(response.Content, MaxResponseBytes, "invalid_response", token);
            using var envelope = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
            var result = ParseResponse(envelope.RootElement, settings.Model);
            return result with { LatencyMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue) };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ModerationAiException("provider_timeout", true);
        }
        catch (HttpRequestException)
        {
            throw new ModerationAiException("provider_unavailable", true);
        }
        catch (IOException)
        {
            throw new ModerationAiException("provider_unavailable", true);
        }
        catch (JsonException)
        {
            throw new ModerationAiException("invalid_response", false);
        }
        finally
        {
            foreach (var name in uploadedGeminiFiles)
                await TryDeleteGeminiFileAsync(name, settings.ApiKey);
            foreach (var path in temporaryFiles)
            {
                try { File.Delete(path); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private async Task LogProviderFailureAsync(HttpResponseMessage response, string model, CancellationToken requestToken, CancellationToken callerToken)
    {
        if (logger is null || !logger.IsEnabled(LogLevel.Warning)) return;
        using var diagnosticTimeout = CancellationTokenSource.CreateLinkedTokenSource(requestToken);
        diagnosticTimeout.CancelAfter(TimeSpan.FromSeconds(2));
        var status = "unknown"; var reason = "unknown"; var parameter = "unknown";
        try
        {
            var bytes = await ReadBoundedAsync(response.Content, 16 * 1024, "diagnostic_too_large", diagnosticTimeout.Token);
            using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
            if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                // Log only known metadata. Provider messages can echo credentials or user content.
                if (error.TryGetProperty("status", out var value) && value.ValueKind == JsonValueKind.String)
                    status = value.GetString() is "INVALID_ARGUMENT" or "FAILED_PRECONDITION" or "UNAUTHENTICATED" or "PERMISSION_DENIED" or
                        "NOT_FOUND" or "RESOURCE_EXHAUSTED" or "INTERNAL" or "UNAVAILABLE" or "DEADLINE_EXCEEDED" ? value.GetString()! : "unknown";
                if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                    foreach (var detail in details.EnumerateArray())
                        if (detail.ValueKind == JsonValueKind.Object && detail.TryGetProperty("reason", out value) && value.ValueKind == JsonValueKind.String &&
                            value.GetString() is "API_KEY_INVALID" or "API_KEY_SERVICE_BLOCKED" or "API_KEY_HTTP_REFERRER_BLOCKED" or "API_KEY_IP_ADDRESS_BLOCKED" or
                            "API_KEY_API_TARGET_BLOCKED" or "SERVICE_DISABLED" or "BILLING_DISABLED" or "CONSUMER_INVALID")
                            reason = value.GetString()!;
                if (error.TryGetProperty("message", out value) && value.ValueKind == JsonValueKind.String)
                {
                    var message = value.GetString()!;
                    parameter = new[] { "thinking_level", "thinking_summaries", "max_output_tokens", "response_format", "mime_type", "schema", "system_instruction", "model", "input", "store" }
                        .FirstOrDefault(name => message.Contains(name, StringComparison.OrdinalIgnoreCase)) ?? "unknown";
                }
            }
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
            // Headers already establish the failure; diagnostic timeout must not change retries.
        }
        catch (Exception error) when (error is JsonException or ModerationAiException or IOException or HttpRequestException)
        {
            // A malformed/truncated diagnostic must not hide the original HTTP status.
        }
        logger.LogWarning("Gemini moderation HTTP {HttpStatusCode}; model {Model}; provider status {ProviderStatus}; reason {ProviderReason}; parameter hint {ParameterHint}.",
            (int)response.StatusCode, model, status, reason, parameter);
    }

    private void ValidateConfiguration(ModerationAiOptions settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || settings.ApiKey.Length > 256 ||
            settings.ApiKey.Any(char.IsControl) || string.IsNullOrEmpty(settings.Model) ||
            !ModelName.IsMatch(settings.Model) || settings.TimeoutSeconds is < 1 or > 300 ||
            string.IsNullOrWhiteSpace(prompt.Version) || prompt.Version.Length > 80 ||
            string.IsNullOrWhiteSpace(prompt.SystemInstruction) || prompt.SystemInstruction.Length > 20_000 ||
            prompt.ResponseSchema.ValueKind != JsonValueKind.Object)
            throw new ModerationAiException("invalid_configuration", false);
    }

    private static Uri[] ValidateInput(ModerationAiInput input, ModerationAiOptions settings)
    {
        if (input is null || input.PostId <= 0 || input.Revision <= 0 || input.Title is null ||
            input.Media is null || input.Ingredients is null || input.Steps is null ||
            input.Ingredients.Any(i => i is null) || input.Steps.Any(s => s is null) ||
            input.Media.Any(m => m is null || m.Id <= 0) ||
            input.Media.Select(m => m.Id).Distinct().Count() != input.Media.Count)
            throw new ModerationAiException("invalid_input", false);
        if (input.Media.Count > 10 || input.Title.Length > 512 || (input.Content?.Length ?? 0) > MaxTextChars ||
            input.Ingredients.Count > 200 || input.Steps.Count > 200 ||
            input.Ingredients.Any(i => i.Length > 2_000) || input.Steps.Any(s => s.Length > 5_000) ||
            (long)input.Title.Length + (input.Content?.Length ?? 0) +
            input.Ingredients.Sum(i => (long)i.Length) + input.Steps.Sum(s => (long)s.Length) > MaxTextChars)
            throw new ModerationAiException("input_too_large", false);

        var uris = new Uri[input.Media.Count];
        for (var i = 0; i < input.Media.Count; i++)
        {
            if (!IsImage(input.Media[i].MediaType) && !IsVideo(input.Media[i].MediaType))
                throw new ModerationAiException("unsupported_media", false);
            if (IsVideo(input.Media[i].MediaType) && input.Media[i].DurationSeconds is int storedDuration && storedDuration > MaxVideoDurationSeconds)
                throw new ModerationAiException("video_too_long", false);
            if (!Uri.TryCreate(input.Media[i].Url, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443 || uri.UserInfo.Length != 0 ||
                uri.Fragment.Length != 0 || uri.HostNameType != UriHostNameType.Dns ||
                uri.Host.EndsWith('.') || !uri.Host.Contains('.') ||
                uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
                !(settings.AllowedMediaHosts ?? []).Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase))
                throw new ModerationAiException("unsafe_media_url", false);
            uris[i] = uri;
        }
        return uris;
    }

    private static bool IsImage(string mediaType) => mediaType is "image" or "image/jpeg" or "image/png" or "image/webp";
    private static bool IsVideo(string mediaType) => mediaType is "video" or "video/mp4" or "video/quicktime" or "video/mov" or "video/webm";

    private async Task<(byte[] Bytes, string MimeType)> DownloadImageAsync(Uri uri, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode || (response.RequestMessage?.RequestUri is { } finalUri && finalUri != uri))
                throw new ModerationAiException("media_unavailable", false);
            var mimeType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
            if (mimeType is not ("image/jpeg" or "image/png" or "image/webp"))
                throw new ModerationAiException("invalid_image", false);
            var bytes = await ReadBoundedAsync(response.Content, MaxImageBytes, "image_too_large", ct);
            if (!HasImageSignature(bytes, mimeType))
                throw new ModerationAiException("invalid_image", false);
            return (bytes, mimeType);
        }
        catch (HttpRequestException ex)
        {
            // SocketsHttpHandler wraps exceptions from the guarded ConnectCallback.
            for (Exception? cause = ex; cause is not null; cause = cause.InnerException)
                if (cause is ModerationAiException { Code: "unsafe_media_url" })
                    throw new ModerationAiException("unsafe_media_url", false);
            throw new ModerationAiException("media_unavailable", false);
        }
        catch (IOException)
        {
            throw new ModerationAiException("media_unavailable", false);
        }
    }

    private async Task<(string Path, string MimeType, double DurationSeconds)> DownloadVideoAsync(Uri uri, string declaredMediaType, CancellationToken ct)
    {
        var path = Path.Combine(Path.GetTempPath(), $"veganhelper-moderation-{Guid.NewGuid():N}.video");
        var keepFile = false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode || (response.RequestMessage?.RequestUri is { } finalUri && finalUri != uri))
                throw new ModerationAiException("media_unavailable", false);
            var mimeType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
            if (!IsVideoMime(mimeType) || declaredMediaType != "video" && !declaredMediaType.Equals(mimeType, StringComparison.OrdinalIgnoreCase))
                throw new ModerationAiException("invalid_media", false);
            if (response.Content.Headers.ContentLength is > MaxVideoBytes)
                throw new ModerationAiException("media_too_large", false);

            await using (var source = await response.Content.ReadAsStreamAsync(ct))
            await using (var destination = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[64 * 1024];
                long total = 0;
                while (true)
                {
                    var read = await source.ReadAsync(buffer.AsMemory(), ct);
                    if (read == 0) break;
                    total = checked(total + read);
                    if (total > MaxVideoBytes)
                        throw new ModerationAiException("media_too_large", false);
                    await destination.WriteAsync(buffer.AsMemory(0, read), ct);
                }
                if (total < 12)
                    throw new ModerationAiException("invalid_media", false);
                destination.Position = 0;
                var signature = new byte[12];
                var signatureLength = await destination.ReadAtLeastAsync(signature, 12, throwOnEndOfStream: false, cancellationToken: ct);
                if (!HasVideoSignature(signature, signatureLength, mimeType))
                    throw new ModerationAiException("invalid_media", false);
                var duration = VideoDurationReader.ReadSeconds(destination, mimeType!);
                if (duration is not double seconds || !double.IsFinite(seconds) || seconds <= 0)
                    throw new ModerationAiException("invalid_media", false);
                if (seconds > MaxVideoDurationSeconds)
                    throw new ModerationAiException("video_too_long", false);
                keepFile = true;
                return (path, mimeType!, seconds);
            }
        }
        catch (HttpRequestException ex)
        {
            // SocketsHttpHandler wraps exceptions from the guarded ConnectCallback.
            for (Exception? cause = ex; cause is not null; cause = cause.InnerException)
                if (cause is ModerationAiException { Code: "unsafe_media_url" })
                    throw new ModerationAiException("unsafe_media_url", false);
            throw new ModerationAiException("media_unavailable", false);
        }
        catch (IOException)
        {
            throw new ModerationAiException("media_unavailable", false);
        }
        finally
        {
            if (!keepFile)
            {
                try { File.Delete(path); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static bool IsVideoMime(string? mimeType) => mimeType is "video/mp4" or "video/quicktime" or "video/mov" or "video/webm";

    private static bool HasVideoSignature(byte[] bytes, int length, string? mimeType) => mimeType switch
    {
        "video/mp4" => length >= 8 && bytes.AsSpan(4, 4).SequenceEqual("ftyp"u8),
        "video/quicktime" or "video/mov" => length >= 8 && Encoding.ASCII.GetString(bytes, 4, 4) is "ftyp" or "moov" or "mdat" or "wide",
        "video/webm" => length >= 4 && bytes.AsSpan(0, 4).SequenceEqual(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }),
        _ => false
    };

    private async Task<(string Name, string Uri, string MimeType)> UploadVideoAsync(string path, string mimeType,
        long postId, long mediaId, ModerationAiOptions settings, List<string> uploadedGeminiFiles, CancellationToken ct)
    {
        // Browsers declare MOV as video/quicktime; Gemini documents the equivalent video/mov MIME.
        mimeType = mimeType == "video/quicktime" ? "video/mov" : mimeType;
        var length = new FileInfo(path).Length;
        using var start = new HttpRequestMessage(HttpMethod.Post, FilesUploadEndpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { file = new { display_name = $"post-{postId}-media-{mediaId}" } }, JsonOptions), Encoding.UTF8, "application/json")
        };
        start.Headers.Add("x-goog-api-key", settings.ApiKey);
        start.Headers.Add("X-Goog-Upload-Protocol", "resumable");
        start.Headers.Add("X-Goog-Upload-Command", "start");
        start.Headers.Add("X-Goog-Upload-Header-Content-Length", length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.Headers.Add("X-Goog-Upload-Header-Content-Type", mimeType);
        using var startResponse = await httpClient.SendAsync(start, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!startResponse.IsSuccessStatusCode)
            await ThrowProviderFailureAsync(startResponse, settings.Model, ct);
        if (!startResponse.Headers.TryGetValues("X-Goog-Upload-URL", out var uploadUrls))
            throw new ModerationAiException("invalid_response", false);
        var uploadUrlValues = uploadUrls.Take(2).ToArray();
        if (uploadUrlValues.Length != 1)
            throw new ModerationAiException("invalid_response", false);
        var uploadUrlText = uploadUrlValues[0];
        if (!Uri.TryCreate(uploadUrlText, UriKind.Absolute, out var uploadUri) ||
            uploadUri.Scheme != Uri.UriSchemeHttps || uploadUri.Port != 443 || uploadUri.UserInfo.Length != 0 ||
            !uploadUri.IdnHost.Equals("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase))
            throw new ModerationAiException("invalid_response", false);

        using var videoStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var upload = new HttpRequestMessage(HttpMethod.Post, uploadUri) { Content = new StreamContent(videoStream, 64 * 1024) };
        upload.Content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        upload.Content.Headers.ContentLength = length;
        upload.Headers.Add("X-Goog-Upload-Offset", "0");
        upload.Headers.Add("X-Goog-Upload-Command", "upload, finalize");
        using var uploadResponse = await httpClient.SendAsync(upload, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!uploadResponse.IsSuccessStatusCode)
            await ThrowProviderFailureAsync(uploadResponse, settings.Model, ct);
        var file = ParseGeminiFile(await ReadBoundedAsync(uploadResponse.Content, MaxResponseBytes, "invalid_response", ct));
        uploadedGeminiFiles.Add(file.Name);
        if (file.State == "FAILED")
            throw new ModerationAiException("invalid_media", false);
        var activeFile = await WaitForGeminiFileAsync(file, settings, ct);
        return (activeFile.Name, activeFile.Uri, activeFile.MimeType);
    }

    private async Task<(string Name, string Uri, string MimeType, string State)> WaitForGeminiFileAsync(
        (string Name, string Uri, string MimeType, string State) file, ModerationAiOptions settings, CancellationToken ct)
    {
        var current = file;
        var deadline = Stopwatch.StartNew();
        while (current.State == "PROCESSING")
        {
            if (deadline.Elapsed >= TimeSpan.FromSeconds(45))
                throw new ModerationAiException("provider_timeout", true);
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
            using var request = new HttpRequestMessage(HttpMethod.Get, FileUri(current.Name));
            request.Headers.Add("x-goog-api-key", settings.ApiKey);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                await ThrowProviderFailureAsync(response, settings.Model, ct);
            current = ParseGeminiFile(await ReadBoundedAsync(response.Content, MaxResponseBytes, "invalid_response", ct));
            if (current.Name != file.Name)
                throw new ModerationAiException("invalid_response", false);
            if (current.State == "FAILED")
                throw new ModerationAiException("invalid_media", false);
        }
        if (current.State != "ACTIVE")
            throw new ModerationAiException("invalid_response", false);
        if (!current.MimeType.StartsWith("video/", StringComparison.Ordinal))
            throw new ModerationAiException("invalid_media", false);
        return current;
    }

    private async Task ThrowProviderFailureAsync(HttpResponseMessage response, string model, CancellationToken ct)
    {
        await LogProviderFailureAsync(response, model, ct, ct);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new ModerationAiException("provider_quota", true,
                response.Headers.RetryAfter?.Delta?.TotalSeconds ??
                (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow)?.TotalSeconds);
        throw new ModerationAiException("provider_error", IsTransient(response.StatusCode));
    }

    private static (string Name, string Uri, string MimeType, string State) ParseGeminiFile(byte[] bytes)
    {
        using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
        var root = json.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("file", out var wrapped))
            root = wrapped;
        if (root.ValueKind != JsonValueKind.Object ||
            !TryReadString(root, "name", out var name) || !Regex.IsMatch(name, @"\Afiles/[A-Za-z0-9_-]{1,200}\z", RegexOptions.CultureInvariant) ||
            !TryReadString(root, "state", out var state) || state is not ("PROCESSING" or "ACTIVE" or "FAILED"))
            throw new ModerationAiException("invalid_response", false);
        // Failed File resources can omit URI/MIME; retain their name so finally can still delete them.
        if (state == "FAILED") return (name, "", "", state);
        if (
            !TryReadString(root, "uri", out var uri) || !Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri) ||
            parsedUri.Scheme != Uri.UriSchemeHttps || parsedUri.Port != 443 || parsedUri.UserInfo.Length != 0 ||
            !parsedUri.IdnHost.Equals("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase) ||
            !TryReadString(root, "mimeType", out var mimeType) || !IsVideoMime(mimeType))
            throw new ModerationAiException("invalid_response", false);
        return (name, uri, mimeType, state);
    }

    private static bool TryReadString(JsonElement element, string name, out string value)
    {
        value = "";
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value = property.GetString()!);
    }

    private static Uri FileUri(string name) => new(FilesApiEndpoint, name);

    private async Task TryDeleteGeminiFileAsync(string name, string apiKey)
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, FileUri(name));
            request.Headers.Add("x-goog-api-key", apiKey);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cleanup.Token);
            // Cleanup is best effort; never replace a moderation result or provider error.
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException or ModerationAiException)
        {
            // Gemini files expire automatically; an unsuccessful cleanup is safe to retry later.
        }
    }

    private static bool HasImageSignature(byte[] bytes, string mimeType) => mimeType switch
    {
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
        "image/png" => bytes.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
        "image/webp" => bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, int maximum, string failureCode, CancellationToken ct)
    {
        if (content.Headers.ContentLength > maximum)
            throw new ModerationAiException(failureCode, false);
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, maximum - (int)output.Length + 1)), ct);
            if (read == 0)
                return output.ToArray();
            if (output.Length + read > maximum)
                throw new ModerationAiException(failureCode, false);
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }

    private ModerationAiResult ParseResponse(JsonElement root, string requestedModel)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw InvalidResponse();
        if (!root.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.String)
            throw InvalidResponse();
        if (status.GetString() != "completed" || root.TryGetProperty("error", out _))
            throw new ModerationAiException("provider_blocked", false);
        if (root.TryGetProperty("errors", out var errors))
        {
            if (errors.ValueKind != JsonValueKind.Array)
                throw InvalidResponse();
            if (errors.GetArrayLength() != 0)
                throw new ModerationAiException("provider_blocked", false);
        }
        if (!root.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array)
            throw InvalidResponse();
        var text = new StringBuilder();
        var outputSteps = 0;
        foreach (var step in steps.EnumerateArray())
        {
            if (step.ValueKind != JsonValueKind.Object || !step.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
                throw InvalidResponse();
            if (step.TryGetProperty("error", out var stepError) && stepError.ValueKind != JsonValueKind.Null)
                throw new ModerationAiException("provider_blocked", false);
            if (type.GetString() == "thought")
                continue;
            if (type.GetString() != "model_output" || ++outputSteps > 1 ||
                !step.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                throw InvalidResponse();
            foreach (var part in content.EnumerateArray())
            {
                if (part.ValueKind != JsonValueKind.Object || !part.TryGetProperty("type", out var partType) ||
                    partType.ValueKind != JsonValueKind.String || partType.GetString() != "text" ||
                    !part.TryGetProperty("text", out var partText) || partText.ValueKind != JsonValueKind.String)
                    throw new ModerationAiException("provider_blocked", false);
                text.Append(partText.GetString());
            }
        }
        if (outputSteps != 1 || text.Length == 0)
            throw InvalidResponse();
        using var document = JsonDocument.Parse(text.ToString(), new JsonDocumentOptions { MaxDepth = 8 });
        var result = document.RootElement;
        RequireProperties(result, "result", "reason");
        var decision = RequiredString(result, "result", 4);
        if (decision is not ("PASS" or "FAIL"))
            throw InvalidResponse();
        var summary = RequiredString(result, "reason", 200);
        var verdict = decision == "PASS" ? "safe" : "flagged";
        // Keep existing Admin DTO/history shapes without inventing confidence, quotes or media attribution.
        IReadOnlyList<ModerationAiFinding> findings = decision == "PASS" ? [] :
            [new("policy_review", "post", "", null, summary)];
        var model = requestedModel;
        if (root.TryGetProperty("model", out var modelJson))
        {
            if (modelJson.ValueKind != JsonValueKind.String || !ModelName.IsMatch(modelJson.GetString() ?? ""))
                throw InvalidResponse();
            model = modelJson.GetString()!;
        }
        int? inputTokens = null;
        int? outputTokens = null;
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.ValueKind != JsonValueKind.Object)
                throw InvalidResponse();
            inputTokens = TokenCount(usage, "total_input_tokens");
            outputTokens = TokenCount(usage, "total_output_tokens");
        }
        return new ModerationAiResult(verdict, null, summary, findings, model, prompt.Version, inputTokens, outputTokens, 0);
    }

    private static int? TokenCount(JsonElement usage, string property)
    {
        if (!usage.TryGetProperty(property, out var value))
            return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var count) || count < 0)
            throw InvalidResponse();
        return count;
    }

    private static string RequiredString(JsonElement element, string name, int maximum, bool allowEmpty = false)
    {
        var value = element.GetProperty(name);
        if (value.ValueKind != JsonValueKind.String)
            throw InvalidResponse();
        var text = value.GetString()!;
        if (text.Length > maximum || (!allowEmpty && string.IsNullOrWhiteSpace(text)))
            throw InvalidResponse();
        return text;
    }

    private static void RequireProperties(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw InvalidResponse();
        var actual = element.EnumerateObject().Select(p => p.Name).ToArray();
        if (actual.Length != expected.Length || actual.Distinct(StringComparer.Ordinal).Count() != expected.Length ||
            actual.Any(p => !expected.Contains(p, StringComparer.Ordinal)))
            throw InvalidResponse();
    }

    private static ModerationAiException InvalidResponse() => new("invalid_response", false);
    private static bool IsTransient(HttpStatusCode status) => status == HttpStatusCode.TooManyRequests || (int)status >= 500;
}
