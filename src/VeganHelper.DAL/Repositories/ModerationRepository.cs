using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Integrations.Moderation;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class ModerationRepository(AppDbContext db) : IModerationRepository
{
    public async Task<DatabasePage<ModerationPostProjection>> ListPostsAsync(string? keyword, string? status, int page, int size, CancellationToken ct)
    {
        var query = db.Posts.AsNoTracking().Where(p => !p.IsDeleted);
        if (status is not null) query = query.Where(p => p.Status == status);
        if (keyword is not null)
        {
            var pattern = "%" + keyword.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(p => EF.Functions.ILike(EF.Functions.Collate(p.Title, "default"), pattern, "\\"));
        }
        var count = await query.LongCountAsync(ct);
        var items = await query.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).Skip((page - 1) * size).Take(size)
            .Select(p => new ModerationPostProjection { Id = p.Id, AuthorId = p.AuthorId, Title = p.Title, PostType = p.PostType,
                Status = p.Status, ContentRevision = p.ContentRevision, CreatedAt = p.CreatedAt,
                ScanState = db.Set<PostModerationScan>().Where(s => s.PostId == p.Id && s.Revision == p.ContentRevision).Select(s => s.State).FirstOrDefault(),
                ConfidenceScore = db.Set<PostModerationScan>().Where(s => s.PostId == p.Id && s.Revision == p.ContentRevision).Select(s => s.Confidence).FirstOrDefault() }).ToListAsync(ct);
        return new(count, items);
    }
    private IQueryable<ModerationFlagProjection> FlagQuery() =>
        from f in db.Flags.AsNoTracking()
        join p in db.Posts on f.PostId equals p.Id
        join s in db.Set<PostModerationScan>() on f.ModerationScanId equals (long?)s.Id into scans
        from s in scans.DefaultIfEmpty()
        where f.SourceType == "ai" && f.Status == "pending" && !p.IsDeleted && (f.PostRevision == null || f.PostRevision == p.ContentRevision)
        select new ModerationFlagProjection { Id = f.Id, PostId = p.Id, Title = p.Title, ContentRevision = p.ContentRevision,
            Reason = f.Reason, AiModelName = f.AiModelName, ConfidenceScore = s == null ? null : s.Confidence,
            FindingsJson = s == null ? "[]" : s.FindingsJson, CreatedAt = f.CreatedAt };
    public async Task<DatabasePage<ModerationFlagProjection>> ListFlagsAsync(long? postId, double? minConfidence, int page, int size, CancellationToken ct)
    {
        var query = FlagQuery();
        if (postId.HasValue) query = query.Where(f => f.PostId == postId);
        if (minConfidence.HasValue) query = query.Where(f => f.ConfidenceScore >= minConfidence);
        var count = await query.LongCountAsync(ct);
        return new(count, await query.OrderBy(f => f.CreatedAt).ThenBy(f => f.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct));
    }
    public Task<List<ModerationFlagProjection>> CurrentFlagsAsync(long postId, CancellationToken ct) => FlagQuery().Where(f => f.PostId == postId).OrderBy(f => f.Id).ToListAsync(ct);
    public Task<Post?> DetailAsync(long postId, CancellationToken ct) => db.Posts.AsNoTracking().Include(p => p.Media).Include(p => p.PostSteps)
        .Include(p => p.PostIngredients).ThenInclude(i => i.Ingredient).AsSplitQuery().SingleOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, ct);
    public Task<Post?> LockPostAsync(long postId, CancellationToken ct) =>
        db.Posts.FromSqlInterpolated($"SELECT * FROM posts WHERE id = {postId} FOR UPDATE").SingleOrDefaultAsync(ct);
    public Task<Flag?> FindFlagAsync(long flagId, CancellationToken ct) => db.Flags.AsNoTracking().SingleOrDefaultAsync(f => f.Id == flagId, ct);
    public Task<List<PostModerationDecision>> DecisionsAsync(long postId, CancellationToken ct) => db.Set<PostModerationDecision>().AsNoTracking()
        .Where(d => d.PostId == postId).OrderByDescending(d => d.Id).Take(20).ToListAsync(ct);
    public Task<PostModerationScan?> CurrentScanAsync(long postId, int revision, CancellationToken ct) => db.Set<PostModerationScan>().AsNoTracking()
        .SingleOrDefaultAsync(s => s.PostId == postId && s.Revision == revision, ct);
    public Task ResolveFlagsAsync(long postId, int revision, long adminId, string note, bool keep, DateTime now, CancellationToken ct) =>
        db.Flags.Where(f => f.PostId == postId && f.SourceType == "ai" && f.Status == "pending" && (f.PostRevision == null || f.PostRevision == revision))
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Status, keep ? "dismissed" : "resolved").SetProperty(f => f.ResolvedByAdminId, adminId)
                .SetProperty(f => f.ResolutionNote, note).SetProperty(f => f.ResolvedAt, now), ct);
    public Task<PostModerationSettings> SettingsAsync(string? lockMode, CancellationToken ct) => lockMode switch
    {
        "update" => db.Set<PostModerationSettings>().FromSqlRaw("SELECT * FROM post_moderation_settings WHERE id = 1 FOR UPDATE").SingleAsync(ct),
        "share" => db.Set<PostModerationSettings>().FromSqlRaw("SELECT * FROM post_moderation_settings WHERE id = 1 FOR SHARE").SingleAsync(ct),
        _ => db.Set<PostModerationSettings>().AsNoTracking().SingleAsync(ct)
    };
    public Task ObsoleteScansAsync(long postId, int revision, DateTime now, CancellationToken ct) => db.Set<PostModerationScan>()
        .Where(s => s.PostId == postId && s.Revision == revision && (s.State == "queued" || s.State == "processing"))
        .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "obsolete").SetProperty(x => x.LeaseToken, (Guid?)null)
            .SetProperty(x => x.LeaseUntil, (DateTime?)null).SetProperty(x => x.CompletedAt, now), ct);
    public void AddDecision(PostModerationDecision d) => db.Add(d);
    public void AddFlag(Flag flag) => db.Add(flag);
    public void AddUsage(AiUsage usage) => db.Add(usage);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public Task<IDbContextTransaction> BeginAsync(CancellationToken ct) => db.Database.BeginTransactionAsync(ct);

    public async Task<PostModerationScan?> ClaimAsync(DateTime now, Guid lease, CancellationToken ct)
    {
        // A recurring job can process multiple posts; locks must refresh rows rather than reuse prior tracked snapshots.
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Set<PostModerationScan>().Where(s => s.State == "processing" && s.LeaseUntil <= now && s.Attempts >= 5)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "failed").SetProperty(x => x.ErrorCode, "lease_exhausted")
                .SetProperty(x => x.CompletedAt, now).SetProperty(x => x.LeaseToken, (Guid?)null).SetProperty(x => x.LeaseUntil, (DateTime?)null), ct);
        var rows = await db.Set<PostModerationScan>().FromSqlInterpolated($"""
            SELECT s.* FROM post_moderation_scans s
            JOIN posts p ON p.id = s.post_id
            WHERE s.attempts < 5 AND p.is_deleted = false AND p.status = 'pending_review' AND p.content_revision = s.revision
              AND ((s.state = 'queued' AND s.next_attempt_at <= {now}) OR (s.state = 'processing' AND s.lease_until <= {now}))
            ORDER BY s.next_attempt_at, s.id LIMIT 1 FOR UPDATE OF s SKIP LOCKED
            """).ToListAsync(ct);
        var scan = rows.SingleOrDefault();
        if (scan is not null) { scan.State = "processing"; scan.Attempts++; scan.LeaseToken = lease; scan.LeaseUntil = now.AddMinutes(3); await db.SaveChangesAsync(ct); }
        await transaction.CommitAsync(ct);
        // Worker finalization must reload the lease under a lock; do not retain a stale tracked claim.
        if (scan is not null) db.Entry(scan).State = EntityState.Detached;
        return scan;
    }
    public Task<PostModerationScan?> LockScanAsync(long scanId, CancellationToken ct) => db.Set<PostModerationScan>()
        .FromSqlInterpolated($"SELECT * FROM post_moderation_scans WHERE id = {scanId} FOR UPDATE").SingleOrDefaultAsync(ct);
    public async Task<ModerationAiInput?> InputAsync(long postId, int revision, CancellationToken ct)
    {
        var post = await DetailAsync(postId, ct);
        if (post is null || post.ContentRevision != revision || post.Status != "pending_review") return null;
        return new(postId, revision, post.Title, post.Content,
            post.Media.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => new ModerationAiMedia(m.Id, m.MediaUrl, m.MediaType)).ToArray(),
            post.PostIngredients.Select(i => $"{i.Ingredient?.Name} {i.Quantity} {i.Unit}").ToArray(),
            post.PostSteps.OrderBy(s => s.StepNumber).Select(s => $"{s.StepNumber}. {s.Description}").ToArray());
    }
}
