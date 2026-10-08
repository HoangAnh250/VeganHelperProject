using System.Text.Json;
using Microsoft.Extensions.Options;
using VeganHelper.BLL.Contracts;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Integrations.Moderation;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class PostModerationProcessor(IModerationRepository repository, IPostModerationAiClient client,
    INotificationPublisher notifications, IOptions<ModerationAiOptions> options, TimeProvider clock)
{
    public async Task RunAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled) return;
        // Persistent scan rows own retry/recovery; Hangfire only wakes the processor.
        for (var i = 0; i < 2; i++)
        {
            ct.ThrowIfCancellationRequested();
            var scan = await repository.ClaimAsync(clock.GetUtcNow().UtcDateTime, Guid.NewGuid(), ct);
            if (scan is null) return;
            ModerationAiResult? result = null;
            ModerationAiException? failure = null;
            var input = await repository.InputAsync(scan.PostId, scan.Revision, ct);
            if (input is not null)
            {
                try { result = await client.ReviewAsync(input, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (ModerationAiException ex) { failure = ex; }
                catch (Exception) { failure = new("provider_error", false); }
            }
            await FinishAsync(scan, input is null, result, failure, ct);
        }
    }
    private async Task FinishAsync(PostModerationScan claimed, bool missingInput, ModerationAiResult? result, ModerationAiException? failure, CancellationToken ct)
    {
        await using var transaction = await repository.BeginAsync(ct);
        // Same order as human decisions: post then scan. Never hold either lock during network calls.
        var post = await repository.LockPostAsync(claimed.PostId, ct);
        var scan = await repository.LockScanAsync(claimed.Id, ct);
        if (scan is null || scan.State != "processing" || scan.LeaseToken != claimed.LeaseToken) return;
        var now = clock.GetUtcNow().UtcDateTime;
        scan.LeaseToken = null; scan.LeaseUntil = null;
        if (missingInput || post is null || post.IsDeleted || post.ContentRevision != scan.Revision || post.Status != "pending_review")
        {
            scan.State = "obsolete"; scan.CompletedAt = now;
            await repository.SaveAsync(ct); await transaction.CommitAsync(ct); return;
        }
        // The HTTP boundary validates the full response. Defend publication independently as well.
        if (result is not null && (!double.IsFinite(result.Confidence) || result.Confidence is < 0 or > 1 ||
            result.Verdict is not ("safe" or "flagged" or "uncertain") || string.IsNullOrWhiteSpace(result.Summary) || result.Summary.Length > 500 ||
            string.IsNullOrWhiteSpace(result.Model) || result.Model.Length > 100 || string.IsNullOrWhiteSpace(result.PromptVersion) || result.PromptVersion.Length > 30 ||
            result.Findings is null || result.Verdict == "safe" && result.Findings.Count != 0 || result.InputTokens < 0 || result.OutputTokens < 0 || result.LatencyMs < 0))
        { result = null; failure = new("invalid_response", false); }
        if (result is null)
        {
            var retry = failure?.Retryable == true && scan.Attempts < 5;
            scan.State = retry ? "queued" : failure?.Retryable == true ? "failed" : "manual_required";
            scan.ErrorCode = SafeCode(failure?.Code);
            scan.Model = options.Value.Model; scan.PromptVersion = "post-moderation.v1";
            scan.NextAttemptAt = now.AddSeconds(30 * Math.Pow(3, Math.Max(0, scan.Attempts - 1)));
            scan.CompletedAt = retry ? null : now;
        }
        else
        {
            scan.Model = result.Model; scan.PromptVersion = result.PromptVersion; scan.Confidence = result.Confidence;
            scan.Summary = result.Summary; scan.FindingsJson = JsonSerializer.Serialize(result.Findings, JsonSerializerOptions.Web);
            scan.InputTokens = result.InputTokens; scan.OutputTokens = result.OutputTokens; scan.LatencyMs = result.LatencyMs;
            scan.ErrorCode = null; scan.CompletedAt = now;
            if (result.Verdict is "flagged" or "uncertain")
            {
                scan.State = "flagged";
                repository.AddFlag(new Flag { PostId = post.Id, PostRevision = scan.Revision, ModerationScanId = scan.Id, SourceType = "ai",
                    AiModelName = result.Model, Reason = result.Summary, Status = "pending", CreatedAt = now });
            }
            else if (!double.IsFinite(options.Value.MinAutoPublishConfidence) || result.Confidence < Math.Max(.9, options.Value.MinAutoPublishConfidence))
            { scan.State = "manual_required"; scan.ErrorCode = "low_confidence"; }
            else if ((await repository.CurrentFlagsAsync(post.Id, ct)).Count > 0)
            { scan.State = "manual_required"; scan.ErrorCode = "pending_ai_flag"; }
            else
            {
                scan.State = "safe";
                // Share lock makes a toggle update and a publication take effect in a defined order.
                var settings = await repository.SettingsAsync("share", ct);
                if (settings.AutoPublishEnabled)
                {
                    post.Status = "published"; post.UpdatedAt = now;
                    var decision = new PostModerationDecision { PostId = post.Id, Revision = scan.Revision, ScanId = scan.Id,
                        Action = "approve", ActorType = "ai", Reason = result.Summary, CreatedAt = now };
                    repository.AddDecision(decision); await repository.SaveAsync(ct);
                    await notifications.NotifyModerationDecisionAsync(decision.Id, ct);
                }
            }
        }
        repository.AddUsage(new AiUsage { RequestId = claimed.LeaseToken!.Value, UserId = post.AuthorId, FeatureType = "content_moderation", Provider = "gemini",
            ModelName = scan.Model!, Status = result is null ? "failed" : "completed", InputTokens = result?.InputTokens, OutputTokens = result?.OutputTokens, CreatedAt = now });
        await repository.SaveAsync(ct); await transaction.CommitAsync(ct);
    }
    private static string SafeCode(string? code) => code is "provider_timeout" or "provider_error" or "provider_unavailable" or "invalid_response" or
        "unsupported_media" or "media_unavailable" or "invalid_media" or "invalid_image" or "unsafe_media_url" or "provider_blocked" or
        "invalid_configuration" or "invalid_input" or "media_too_large" or "input_too_large" or "request_too_large" or "ai_disabled"
        ? code : "review_unavailable";
}
