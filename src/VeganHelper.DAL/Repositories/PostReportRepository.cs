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
    private IQueryable<AdminPostReportProjection> ReportQuery(string? status, long? postId)
    {
        var q = db.Flags.AsNoTracking().Where(f => f.SourceType == "user" && f.PostId != null);
        if (status is not null) q = q.Where(f => f.Status == status);
        if (postId.HasValue) q = q.Where(f => f.PostId == postId);

        return
            from f in q
            join p in db.Posts.AsNoTracking() on f.PostId equals (long?)p.Id into posts
            from p in posts.DefaultIfEmpty()
            join author in db.Users.AsNoTracking() on p.AuthorId equals author.Id into authors
            from author in authors.DefaultIfEmpty()
            join authorProfile in db.UserProfiles.AsNoTracking() on author.Id equals authorProfile.UserId into authorProfiles
            from authorProfile in authorProfiles.DefaultIfEmpty()
            join reporter in db.Users.AsNoTracking() on f.ReporterId equals (long?)reporter.Id into reporters
            from reporter in reporters.DefaultIfEmpty()
            join reporterProfile in db.UserProfiles.AsNoTracking() on reporter.Id equals reporterProfile.UserId into reporterProfiles
            from reporterProfile in reporterProfiles.DefaultIfEmpty()
            select new AdminPostReportProjection
            {
                Id = f.Id,
                PostId = f.PostId,
                PostTitle = p == null ? "" : p.Title,
                ReportCount = db.Flags.Count(other => other.SourceType == "user" && other.PostId == f.PostId && other.PostRevision == f.PostRevision && other.Status == f.Status),
                AuthorId = p == null ? 0 : p.AuthorId,
                AuthorName = author == null ? "Unknown" : (authorProfile == null || authorProfile.DisplayName == "" ? author.Username : authorProfile.DisplayName),
                ReporterId = f.ReporterId,
                ReporterName = reporter == null ? "Unknown" : (reporterProfile == null || reporterProfile.DisplayName == "" ? reporter.Username : reporterProfile.DisplayName),
                PostRevision = f.PostRevision,
                Reason = f.Reason,
                ReasonCategory = f.ReasonCategory,
                Status = f.Status,
                CreatedAt = f.CreatedAt,
                ResolutionNote = f.ResolutionNote,
                ReportedSnapshotJson = f.ReportedSnapshotJson
            };
    }
    public async Task<DatabasePage<AdminPostReportProjection>> ListAsync(string? status, long? postId, int page, int size, CancellationToken ct)
    {
        var query = ReportQuery(status, postId);
        // Filtered detail requests retain every report; the queue has one row per post revision/status.
        if (!postId.HasValue)
            query = query.Where(report => report.Id == db.Flags.Where(other => other.SourceType == "user" &&
                other.PostId == report.PostId && other.PostRevision == report.PostRevision && other.Status == report.Status)
                .Max(other => other.Id));
        var count = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(f => f.CreatedAt).ThenByDescending(f => f.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(count, items);
    }
    public async Task<IReadOnlyList<AdminPostReportProjection>> ListRevisionAsync(long postId, int? revision, string status, CancellationToken ct) =>
        await ReportQuery(status, postId).Where(report => report.PostRevision == revision)
            .OrderByDescending(report => report.CreatedAt).ThenByDescending(report => report.Id).Take(100).ToListAsync(ct);

    public Task<AdminPostReportProjection?> GetReportProjectionAsync(long id, CancellationToken ct) =>
        ReportQuery(null, null).SingleOrDefaultAsync(r => r.Id == id, ct);
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
