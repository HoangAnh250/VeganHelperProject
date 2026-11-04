using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class PostSummaryRepository(AppDbContext db) : IPostSummaryRepository
{
    public async Task<SummarySource?> GetPublicSourceAsync(long postId, CancellationToken ct)
    {
        var post = await db.Posts.AsNoTracking()
            .Where(p => p.Id == postId && !p.IsDeleted && p.Status == "published" && !p.ModerationHold)
            .Select(p => new { p.Id, p.ContentRevision })
            .SingleOrDefaultAsync(ct);
        if (post is null) return null;
        var videos = await db.PostMedia.AsNoTracking()
            .Where(m => m.PostId == postId && m.MediaType == "video" && m.ProcessingStatus == "ready")
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id)
            .Select(m => new SummaryVideo(m.Id, m.MediaUrl, m.DisplayOrder))
            .ToListAsync(ct);
        return new SummarySource(post.Id, post.ContentRevision, videos);
    }

    public Task<PostSummary?> GetAsync(long postId, CancellationToken ct) =>
        db.PostSummaries.AsNoTracking().SingleOrDefaultAsync(s => s.PostId == postId, ct);

    public Task AddAsync(PostSummary summary, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO post_summaries (post_id, source_media_ids_json, content_revision, status, requested_at)
            VALUES ({summary.PostId}, {summary.SourceMediaIdsJson}, {summary.ContentRevision},
                    {summary.Status}, {summary.RequestedAt})
            ON CONFLICT (post_id) DO NOTHING
            """, ct);

    public Task ResetAsync(long postId, int revision, string mediaIdsJson, CancellationToken ct) =>
        db.PostSummaries.Where(s => s.PostId == postId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ContentRevision, revision)
                .SetProperty(x => x.SourceMediaIdsJson, mediaIdsJson)
                .SetProperty(x => x.Status, "pending")
                .SetProperty(x => x.Transcript, (string?)null)
                .SetProperty(x => x.AiGeneratedText, (string?)null)
                .SetProperty(x => x.ModelName, (string?)null)
                .SetProperty(x => x.ErrorMessage, (string?)null)
                .SetProperty(x => x.GeneratedAt, (DateTime?)null)
                .SetProperty(x => x.RequestedAt, DateTime.UtcNow), ct);

    public async Task<bool> TryClaimAsync(long postId, int revision, string mediaIdsJson, CancellationToken ct) =>
        await db.PostSummaries.Where(s => s.PostId == postId && s.ContentRevision == revision &&
                s.SourceMediaIdsJson == mediaIdsJson && s.Status == "pending")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "processing"), ct) == 1;

    public async Task<bool> CompleteAsync(long postId, int revision, string mediaIdsJson, string transcript,
        string summary, string model, CancellationToken ct) =>
        await db.PostSummaries.Where(s => s.PostId == postId && s.ContentRevision == revision &&
                s.SourceMediaIdsJson == mediaIdsJson && s.Status == "processing")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "completed")
                .SetProperty(x => x.Transcript, transcript)
                .SetProperty(x => x.AiGeneratedText, summary)
                .SetProperty(x => x.ModelName, model)
                .SetProperty(x => x.ErrorMessage, (string?)null)
                .SetProperty(x => x.GeneratedAt, DateTime.UtcNow), ct) == 1;

    public Task FailAsync(long postId, int revision, string mediaIdsJson, string error, CancellationToken ct) =>
        db.PostSummaries.Where(s => s.PostId == postId && s.ContentRevision == revision &&
                s.SourceMediaIdsJson == mediaIdsJson && s.Status == "processing")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "failed")
                .SetProperty(x => x.ErrorMessage, error), ct);
}
