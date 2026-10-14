using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class PostReportRepository(AppDbContext db) : IPostReportRepository
{
    public Task<IDbContextTransaction> BeginAsync(CancellationToken ct) => db.Database.BeginTransactionAsync(ct);
    public Task<User?> LockReporterAsync(long id, CancellationToken ct) =>
        db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    public Task<Flag?> PendingAsync(long reporterId, long postId, int revision, CancellationToken ct) =>
        db.Flags.AsNoTracking().SingleOrDefaultAsync(f => f.SourceType == "user" && f.Status == "pending" &&
            f.ReporterId == reporterId && f.PostId == postId && f.PostRevision == revision, ct);
    public Task<int> RecentCountAsync(long reporterId, DateTime since, CancellationToken ct) =>
        db.Flags.CountAsync(f => f.SourceType == "user" && f.ReporterId == reporterId && f.CreatedAt >= since, ct);
    public async Task<DatabasePage<Flag>> ListAsync(string? status, long? postId, int page, int size, CancellationToken ct)
    {
        var q = db.Flags.AsNoTracking().Where(f => f.SourceType == "user" && f.PostId != null);
        if (status is not null) q = q.Where(f => f.Status == status);
        if (postId.HasValue) q = q.Where(f => f.PostId == postId);
        return new(await q.LongCountAsync(ct), await q.OrderBy(f => f.CreatedAt).ThenBy(f => f.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct));
    }
    public Task<Flag?> FindAsync(long id, bool forUpdate, CancellationToken ct) => forUpdate
        ? db.Flags.FromSqlInterpolated($"SELECT * FROM flags WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
        : db.Flags.AsNoTracking().SingleOrDefaultAsync(f => f.Id == id, ct);
    public Task<List<PostModerationAssessment>> HistoryAsync(long postId, CancellationToken ct) => db.Set<PostModerationAssessment>()
        .AsNoTracking().Where(a => a.PostId == postId).OrderByDescending(a => a.Id).Take(20).ToListAsync(ct);
    public void Add(Flag report) => db.Add(report);
    public Task<Post?> PostAsync(long id, CancellationToken ct) => db.Posts.AsNoTracking().Include(p => p.Media)
        .Include(p => p.PostIngredients).ThenInclude(i => i.Ingredient).Include(p => p.PostSteps)
        .AsSplitQuery().SingleOrDefaultAsync(p => p.Id == id, ct);
    public async Task RequestRecheckAsync(Post post, DateTime now, CancellationToken ct)
    {
        // Caller holds the post row. Match worker/Admin lock order: post, then scan.
        var scans = await db.Set<PostModerationScan>().FromSqlInterpolated($"""
            SELECT * FROM post_moderation_scans WHERE post_id = {post.Id} AND revision = {post.ContentRevision} FOR UPDATE
            """).ToListAsync(ct);
        var scan = scans.SingleOrDefault();
        if (scan is null)
        {
            db.Add(new PostModerationScan { PostId = post.Id, Revision = post.ContentRevision,
                Priority = 1, ReportRecheckRequested = true, CreatedAt = now, NextAttemptAt = now });
        }
        else if (!scan.ReportRecheckRequested)
        {
            scan.ReportRecheckRequested = true; scan.Priority = 1;
            if (scan.State is not ("queued" or "processing"))
            {
                scan.State = "queued"; scan.Attempts = 0; scan.NextAttemptAt = now;
                scan.CompletedAt = null; scan.LeaseToken = null; scan.LeaseUntil = null;
                // Previous completed assessments are immutable in their own table.
            }
        }
    }
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
