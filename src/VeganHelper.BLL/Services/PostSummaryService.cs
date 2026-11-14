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
    private string ProcessingModel => $"{ai.SummaryModel}|speech-v3-language";

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
            var languages = new List<(string Language, int Weight)>();
            var clipNumber = 0;
            foreach (var video in source.Videos)
            {
                clipNumber++;
                var speech = await ai.TranscribeAsync(video.Url, ct);
                var text = speech.Text.Trim();
                if (text.Length == 0) continue;
                languages.Add((ResolveLanguage(speech), text.Count(char.IsLetter)));
                transcript.AppendLine($"Clip {clipNumber} (media {video.Id}):");
                transcript.AppendLine(text).AppendLine();
            }
            var combined = transcript.ToString().Trim();
            if (combined.Length == 0)
                throw new InvalidOperationException("No reliable cooking speech was detected in these videos.");
            if (combined.Length > 80_000)
                throw new InvalidOperationException("The combined transcript is too long to summarize.");
            // Pick once for the whole post; all chunks and the final merge must use the same language.
            // GroupBy and the stable sort preserve the first spoken clip's language on an exact tie.
            var language = languages.GroupBy(item => item.Language)
                .OrderByDescending(group => group.Sum(item => (long)item.Weight)).First().Key;
            var summary = await SummarizeAsync(combined, language, ct);
            var latest = await repository.GetPublicSourceAsync(postId, ct);
            if (latest is null || latest.Revision != source.Revision || SourceIds(latest) != ids) return;
            await repository.CompleteAsync(postId, source.Revision, ids, combined, summary, ProcessingModel, ct);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not summarize post {PostId}.", postId);
            await repository.FailAsync(postId, source.Revision, ids,
                exception is InvalidOperationException && exception.Message.StartsWith("No reliable cooking speech", StringComparison.Ordinal)
                    ? "No reliable cooking speech was detected. Music-only or silent videos cannot be summarized."
                    : "Transcription or summary failed. Please try again later.", CancellationToken.None);
        }
    }

    private static string SourceIds(SummarySource source) =>
        JsonSerializer.Serialize(source.Videos.Select(v => v.Id));

    private static string ResolveLanguage(VideoTranscription speech)
    {
        var language = speech.Language?.Trim().ToLowerInvariant();
        if (language is "vi" or "vietnamese") return "vi";
        if (language is "en" or "english") return "en";
        // A missing language hint must not make the synthetic English clip labels determine the result.
        const string vietnameseLetters = "ăâđêôơưĂÂĐÊÔƠƯàáảãạằắẳẵặầấẩẫậèéẻẽẹềếểễệìíỉĩịòóỏõọồốổỗộờớởỡợùúủũụừứửữựỳýỷỹỵ";
        return speech.Text.Normalize().ToLowerInvariant().Any(vietnameseLetters.Contains) ? "vi" : "en";
    }

    private async Task<string> SummarizeAsync(string transcript, string language, CancellationToken ct)
    {
        const int chunkSize = 16_000;
        if (transcript.Length <= chunkSize) return await ai.SummarizeAsync(transcript, language, ct);
        var partials = new List<string>();
        for (var offset = 0; offset < transcript.Length;)
        {
            var end = Math.Min(offset + chunkSize, transcript.Length);
            if (end < transcript.Length)
            {
                var boundary = transcript.LastIndexOf(' ', end - 1, end - offset);
                if (boundary > offset + chunkSize / 2) end = boundary + 1;
            }
            partials.Add(await ai.SummarizeAsync(transcript[offset..end], language, ct));
            offset = end;
        }
        var combined = string.Join("\n\n", partials);
        if (combined.Length > 24_000)
            throw new InvalidOperationException("Intermediate summaries are too long.");
        return await ai.SummarizeAsync(combined, language, ct);
    }

    private bool Matches(PostSummary summary, SummarySource source) =>
        summary.ContentRevision == source.Revision && summary.SourceMediaIdsJson == SourceIds(source) &&
        (summary.Status != "completed" || summary.ModelName == ProcessingModel);
    private static PostSummaryDto Map(PostSummary summary) =>
        new(summary.PostId, summary.Status, summary.Transcript, summary.AiGeneratedText,
            summary.ErrorMessage, summary.RequestedAt, summary.GeneratedAt);
}
