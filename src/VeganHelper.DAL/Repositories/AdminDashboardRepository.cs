using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class AdminDashboardRepository(AppDbContext db) : IAdminDashboardRepository
{
    public async Task<DashboardOverviewProjection> OverviewAsync(DateTime start, DateTime until, DateTime previousFrom, CancellationToken ct)
    {
        // One parameterized statement: aggregates run in PostgreSQL with one network round trip.
        // Keep every count in the same statement snapshot, including overlapping report queues.
        var rows = await db.Database.SqlQuery<DashboardOverviewProjection>($"""
            WITH member_stats AS (
                SELECT count(*) AS total,
                    count(*) FILTER (WHERE u.created_at >= {start} AND u.created_at < {until}) AS current_count,
                    count(*) FILTER (WHERE u.created_at >= {previousFrom} AND u.created_at < {start}) AS previous_count
                FROM users u JOIN roles r ON u.role_id = r.id
                WHERE u.deleted_at IS NULL AND r.role_name = 'member'
            ), post_stats AS (
                SELECT count(*) FILTER (WHERE status = 'published' AND NOT moderation_hold) AS published,
                    count(*) FILTER (WHERE created_at >= {start} AND created_at < {until}) AS created
                FROM posts WHERE NOT is_deleted
            ), pending_post_flags AS (
                SELECT p.id,
                    f.source_type = 'ai' AND (p.moderation_hold OR f.post_revision IS NULL OR f.post_revision = p.content_revision) AS ai,
                    f.source_type = 'user' AS reported
                FROM flags f JOIN posts p ON f.post_id = p.id
                WHERE f.status = 'pending' AND NOT p.is_deleted
            ), flag_stats AS (
                SELECT count(DISTINCT id) FILTER (WHERE ai) AS ai_posts,
                    count(DISTINCT id) FILTER (WHERE reported) AS reported_posts,
                    count(DISTINCT id) FILTER (WHERE ai OR reported) AS unique_posts
                FROM pending_post_flags
            ), comment_stats AS (
                SELECT count(DISTINCT c.id) AS reported_comments
                FROM flags f JOIN comments c ON f.comment_id = c.id
                WHERE f.source_type = 'user' AND f.status = 'pending' AND NOT c.is_deleted
            )
            SELECT m.total AS "Members", m.current_count AS "NewMembers", m.previous_count AS "PreviousNewMembers",
                p.published AS "PublishedPosts", p.created AS "CreatedPosts", f.ai_posts AS "AiPosts",
                f.reported_posts AS "ReportedPosts", c.reported_comments AS "ReportedComments",
                f.unique_posts + c.reported_comments AS "PendingContent"
            FROM member_stats m CROSS JOIN post_stats p CROSS JOIN flag_stats f CROSS JOIN comment_stats c
            """).ToListAsync(ct);
        return rows.Single();
    }

    public async Task<List<DashboardDailyCount>> ActivityAsync(DateTime start, DateTime until, CancellationToken ct)
    {
        var rows = await db.Posts.AsNoTracking().Where(p => !p.IsDeleted && p.CreatedAt >= start && p.CreatedAt < until)
            .GroupBy(p => p.CreatedAt.Date).Select(g => new { Day = g.Key, Count = g.LongCount() })
            .OrderBy(p => p.Day).ToListAsync(ct);
        return rows.Select(row => new DashboardDailyCount(row.Day, row.Count)).ToList();
    }

    public async Task<DashboardCategoryProjection> CategoriesAsync(DateTime start, DateTime until, int limit, CancellationToken ct)
    {
        var links = from link in db.PostCategories.AsNoTracking()
                    join post in db.Posts on link.PostId equals post.Id
                    join category in db.Categories on link.CategoryId equals category.Id
                    where !post.IsDeleted && post.Status == "published" && !post.ModerationHold
                        && post.CreatedAt >= start && post.CreatedAt < until && category.IsActive
                    select new { post.Id, CategoryId = category.Id, category.Name };
        var items = await links.GroupBy(p => new { p.CategoryId, p.Name })
            .Select(g => new { g.Key.CategoryId, g.Key.Name, Count = g.LongCount(),
                Total = links.Select(p => p.Id).Distinct().LongCount() })
            .OrderByDescending(p => p.Count).ThenBy(p => p.CategoryId).Take(limit).ToListAsync(ct);
        return new(items.FirstOrDefault()?.Total ?? 0, items.Select(item => new DashboardCategoryCount(item.CategoryId, item.Name, item.Count)).ToList());
    }
}
