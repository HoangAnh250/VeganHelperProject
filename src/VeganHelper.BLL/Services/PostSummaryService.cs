using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VeganHelper.BLL.Contracts;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class PostSummaryService(IPostSummaryRepository repository, IVideoSummaryAiClient ai,
    IPostSummaryQueue queue, ILogger<PostSummaryService> logger) : IPostSummaryService
{
    public async Task<PostSummaryDto?> GetAsync(long postId, CancellationToken ct)
    {
        var source = await repository.GetPublicSourceAsync(postId, ct);
        if (source is null) return null;
        var saved = await repository.GetAsync(postId, ct);
        if (saved is null) return new(postId, "not_requested", null, null, null, null, null);
        if (!Matches(saved, source))
            return new(postId, "stale", null, null, null, saved.RequestedAt, null);
        return Map(saved);
    }

    public async Task<PostSummaryDto?> RequestAsync(long postId, CancellationToken ct)
    {
        var source = await repository.GetPublicSourceAsync(postId, ct);
        if (source is null) return null;
        if (source.Videos.Count == 0) throw new ArgumentException("This post has no ready video to summarize.");
        var ids = SourceIds(source);
        var saved = await repository.GetAsync(postId, ct);
        if (saved is null)
        {
            saved = new PostSummary
            {
                PostId = postId, ContentRevision = source.Revision, SourceMediaIdsJson = ids,
                Status = "pending", RequestedAt = DateTime.UtcNow
            };
            await repository.AddAsync(saved, ct);
            saved = (await repository.GetAsync(postId, ct))!;
        }
        if (!Matches(saved, source) || saved.Status is "failed" or "stale")
        {
            await repository.ResetAsync(postId, source.Revision, ids, ct);
            saved = (await repository.GetAsync(postId, ct))!;
        }
        if (saved.Status == "pending") queue.Enqueue(postId);
        return Map(saved);
    }

    public async Task ProcessAsync(long postId, CancellationToken ct)
    {
        var source = await repository.GetPublicSourceAsync(postId, ct);
        if (source is null || source.Videos.Count == 0) return;
        var ids = SourceIds(source);
        if (!await repository.TryClaimAsync(postId, source.Revision, ids, ct)) return;
        try
        {
            var transcript = new StringBuilder();
            var clipNumber = 0;
            foreach (var video in source.Videos)
            {
                var text = (await ai.TranscribeAsync(video.Url, ct)).Trim();
                if (text.Length == 0) throw new InvalidOperationException("One video has no detectable speech.");
                transcript.AppendLine($"Clip {++clipNumber} (media {video.Id}):");
                transcript.AppendLine(text).AppendLine();
            }
            var combined = transcript.ToString().Trim();
            if (combined.Length > 80_000)
                throw new InvalidOperationException("The combined transcript is too long to summarize.");
            var summary = await SummarizeAsync(combined, ct);
            var latest = await repository.GetPublicSourceAsync(postId, ct);
            if (latest is null || latest.Revision != source.Revision || SourceIds(latest) != ids) return;
            await repository.CompleteAsync(postId, source.Revision, ids, combined, summary, ai.SummaryModel, ct);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not summarize post {PostId}.", postId);
            await repository.FailAsync(postId, source.Revision, ids,
                "Transcription or summary failed. Please try again later.", CancellationToken.None);
        }
    }

    private static string SourceIds(SummarySource source) =>
        JsonSerializer.Serialize(source.Videos.Select(v => v.Id));

    private async Task<string> SummarizeAsync(string transcript, CancellationToken ct)
    {
        const int chunkSize = 16_000;
        if (transcript.Length <= chunkSize) return await ai.SummarizeAsync(transcript, ct);
        var partials = new List<string>();
        for (var offset = 0; offset < transcript.Length;)
        {
            var end = Math.Min(offset + chunkSize, transcript.Length);
            if (end < transcript.Length)
            {
                var boundary = transcript.LastIndexOf(' ', end - 1, end - offset);
                if (boundary > offset + chunkSize / 2) end = boundary + 1;
            }
            partials.Add(await ai.SummarizeAsync(transcript[offset..end], ct));
            offset = end;
        }
        var combined = string.Join("\n\n", partials);
        if (combined.Length > 24_000)
            throw new InvalidOperationException("Intermediate summaries are too long.");
        return await ai.SummarizeAsync(combined, ct);
    }

    private static bool Matches(PostSummary summary, SummarySource source) =>
        summary.ContentRevision == source.Revision && summary.SourceMediaIdsJson == SourceIds(source);
    private static PostSummaryDto Map(PostSummary summary) =>
        new(summary.PostId, summary.Status, summary.Transcript, summary.AiGeneratedText,
            summary.ErrorMessage, summary.RequestedAt, summary.GeneratedAt);
}
