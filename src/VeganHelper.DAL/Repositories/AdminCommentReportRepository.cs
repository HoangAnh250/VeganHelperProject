using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class AdminCommentReportRepository(AppDbContext db) : IAdminCommentReportRepository
{
    private IQueryable<AdminCommentReportProjection> Query(string? status, bool onePerComment)
    {
        var reports = db.Flags.AsNoTracking().Where(flag =>
            (flag.SourceType == "user" || flag.SourceType == "keyword") && flag.CommentId != null);
        if (status is not null) reports = reports.Where(flag => flag.Status == status);

        return
            from report in reports
            join comment in db.Comments.AsNoTracking() on report.CommentId equals comment.Id
            join post in db.Posts.AsNoTracking() on comment.PostId equals post.Id
            join author in db.Users.AsNoTracking() on comment.UserId equals author.Id
            join authorProfile in db.UserProfiles.AsNoTracking() on author.Id equals authorProfile.UserId into authorProfiles
            from authorProfile in authorProfiles.DefaultIfEmpty()
            let reporterName = db.Users.Where(user => user.Id == report.ReporterId)
                .Select(user => db.UserProfiles.Where(profile => profile.UserId == user.Id && profile.DisplayName != "")
                    .Select(profile => profile.DisplayName).FirstOrDefault() ?? user.Username)
                .FirstOrDefault()
            where !onePerComment || report.Id == reports.Where(other => other.CommentId == report.CommentId).Min(other => other.Id)
            select new AdminCommentReportProjection
            {
                Id = report.Id,
                CommentId = comment.Id,
                PostId = post.Id,
                PostTitle = post.Title,
                AuthorId = author.Id,
                AuthorName = authorProfile == null || authorProfile.DisplayName == "" ? author.Username : authorProfile.DisplayName,
                CommentContent = comment.Content,
                ReporterId = report.ReporterId,
                ReporterName = report.SourceType == "keyword" ? "Keyword filter" : reporterName ?? "Unknown",
                Reason = report.Reason,
                Status = report.Status,
                ReportCount = db.Flags.Count(other => (other.SourceType == "user" || other.SourceType == "keyword") && other.CommentId == report.CommentId && other.Status == "pending"),
                CreatedAt = report.CreatedAt,
                ResolvedAt = report.ResolvedAt,
                ResolutionNote = report.ResolutionNote
            };
    }

    public async Task<DatabasePage<AdminCommentReportProjection>> ListAsync(string? status, int pageIndex, int pageSize, CancellationToken ct)
    {
        var query = Query(status, onePerComment: true);
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(total, items);
    }

    public Task<AdminCommentReportProjection?> FindAsync(long id, CancellationToken ct) =>
        Query(null, onePerComment: false).SingleOrDefaultAsync(report => report.Id == id, ct);

    public Task<Flag?> LockReportAsync(long id, CancellationToken ct) =>
        db.Flags.FromSqlInterpolated($"SELECT * FROM flags WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);

    public Task<Comment?> LockCommentAsync(long id, CancellationToken ct) =>
        db.Comments.FromSqlInterpolated($"SELECT * FROM comments WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);

    public Task ResolvePendingAsync(long commentId, string status, long adminId, string note, DateTime now, CancellationToken ct) =>
        db.Flags.Where(flag => (flag.SourceType == "user" || flag.SourceType == "keyword") && flag.CommentId == commentId && flag.Status == "pending")
            .ExecuteUpdateAsync(update => update
                .SetProperty(flag => flag.Status, status)
                .SetProperty(flag => flag.ResolvedByAdminId, adminId)
                .SetProperty(flag => flag.ResolutionNote, note)
                .SetProperty(flag => flag.ResolvedAt, now), ct);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
