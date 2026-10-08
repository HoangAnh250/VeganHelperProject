using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace VeganHelper.DAL.Integrations.Moderation;

public sealed class GeminiPostModerationClient(HttpClient httpClient, IOptions<ModerationAiOptions> options,
    ModerationPromptDefinition prompt) : IPostModerationAiClient
{
    private const int MaxImageBytes = 5 * 1024 * 1024;
    private const int MaxTotalImageBytes = 20 * 1024 * 1024;
    // The provider's inline-data limit includes the base64 payload, instructions and text.
    private const int MaxRequestBytes = 20_000_000;
    private const int MaxResponseBytes = 256 * 1024;
    private const int MaxTextChars = 100_000;
    private static readonly Uri Endpoint = new("https://generativelanguage.googleapis.com/v1beta/interactions");
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;
    private static readonly Regex ModelName = new("\\Agemini-[a-z0-9][a-z0-9.-]{0,79}\\z", RegexOptions.CultureInvariant);
    private static readonly Regex FindingCode = new("\\A[a-z][a-z0-9_]{0,63}\\z", RegexOptions.CultureInvariant);

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
                        media = input.Media.Select(m => new { m.Id, m.MediaType }).ToArray()
                    }, JsonOptions)
                }
            };
            var totalBytes = 0;
            for (var index = 0; index < input.Media.Count; index++)
            {
                var image = await DownloadImageAsync(mediaUris[index], token);
                totalBytes = checked(totalBytes + image.Bytes.Length);
                if (totalBytes > MaxTotalImageBytes)
                    throw new ModerationAiException("input_too_large", false);
                // A label immediately before each image makes its database identifier unambiguous.
                parts.Add(new { type = "text", text = JsonSerializer.Serialize(new { mediaId = input.Media[index].Id }) });
                parts.Add(new { type = "image", data = Convert.ToBase64String(image.Bytes), mime_type = image.MimeType });
            }

            var body = JsonSerializer.SerializeToUtf8Bytes(new
            {
                model = settings.Model,
                store = false,
                system_instruction = prompt.SystemInstruction,
                input = parts,
                response_format = new { type = "text", mime_type = "application/json", schema = prompt.ResponseSchema },
                generation_config = new { max_output_tokens = 8192 }
            }, JsonOptions);
            if (body.Length > MaxRequestBytes)
                throw new ModerationAiException("input_too_large", false);

            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = new ByteArrayContent(body) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Headers.Add("x-goog-api-key", settings.ApiKey);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode)
                throw new ModerationAiException("provider_error", IsTransient(response.StatusCode));
            var bytes = await ReadBoundedAsync(response.Content, MaxResponseBytes, "invalid_response", token);
            using var envelope = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
            var result = ParseResponse(envelope.RootElement, input, settings.Model);
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
    }

    private void ValidateConfiguration(ModerationAiOptions settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || settings.ApiKey.Length > 256 ||
            settings.ApiKey.Any(char.IsControl) || string.IsNullOrEmpty(settings.Model) ||
            !ModelName.IsMatch(settings.Model) || settings.TimeoutSeconds is < 1 or > 300 ||
            !double.IsFinite(settings.MinAutoPublishConfidence) || settings.MinAutoPublishConfidence is < 0 or > 1 ||
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
            if (input.Media[i].MediaType is not ("image" or "image/jpeg" or "image/png" or "image/webp"))
                throw new ModerationAiException("unsupported_media", false);
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

    private ModerationAiResult ParseResponse(JsonElement root, ModerationAiInput input, string requestedModel)
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
        RequireProperties(result, "verdict", "confidence", "summary", "findings");
        var verdict = RequiredString(result, "verdict", 9);
        if (verdict is not ("safe" or "flagged" or "uncertain") ||
            result.GetProperty("confidence").ValueKind != JsonValueKind.Number ||
            !result.GetProperty("confidence").TryGetDouble(out var confidence) || !double.IsFinite(confidence) ||
            confidence is < 0 or > 1)
            throw InvalidResponse();
        var summary = RequiredString(result, "summary", 500);
        var findingsJson = result.GetProperty("findings");
        if (findingsJson.ValueKind != JsonValueKind.Array || findingsJson.GetArrayLength() > 20 ||
            (verdict == "safe" ? findingsJson.GetArrayLength() != 0 : findingsJson.GetArrayLength() == 0))
            throw InvalidResponse();
        var findings = new List<ModerationAiFinding>();
        var mediaIds = input.Media.Select(m => m.Id).ToHashSet();
        foreach (var finding in findingsJson.EnumerateArray())
        {
            RequireProperties(finding, "code", "field", "excerpt", "mediaId", "reason");
            var code = RequiredString(finding, "code", 64);
            var field = RequiredString(finding, "field", 20);
            var excerpt = RequiredString(finding, "excerpt", 500, allowEmpty: true);
            var reason = RequiredString(finding, "reason", 1000);
            var id = finding.GetProperty("mediaId");
            long? mediaId = null;
            if (id.ValueKind != JsonValueKind.Null)
            {
                if (id.ValueKind != JsonValueKind.Number || !id.TryGetInt64(out var parsedId))
                    throw InvalidResponse();
                mediaId = parsedId;
            }
            if (!FindingCode.IsMatch(code) || field is not ("title" or "content" or "ingredients" or "steps" or "media") ||
                (field == "media" ? !mediaId.HasValue || !mediaIds.Contains(mediaId.Value) : mediaId.HasValue))
                throw InvalidResponse();
            findings.Add(new ModerationAiFinding(code, field, excerpt, mediaId, reason));
        }
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
        return new ModerationAiResult(verdict, confidence, summary, findings, model, prompt.Version, inputTokens, outputTokens, 0);
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
