using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class AdminAuditRepository(AppDbContext db) : IAdminAuditRepository
{
    public Task<bool> IsActiveAdminAsync(long userId, DateTime now, CancellationToken ct) =>
        (from user in db.Users.AsNoTracking()
         join role in db.Roles on user.RoleId equals role.Id
         where user.Id == userId && user.IsActive && user.DeletedAt == null
            && (user.LockedUntil == null || user.LockedUntil <= now) && role.RoleName == "admin"
            && !db.UserBans.Any(b => b.UserId == user.Id && b.UnbannedAt == null && (b.ExpiresAt == null || b.ExpiresAt > now))
         select user.Id).AnyAsync(ct);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("An audited operation must own its transaction. Call the audit wrapper before starting other transactions.");
        return db.Database.BeginTransactionAsync(ct);
    }

    public async Task AppendAsync(AdminAuditLog entry, CancellationToken ct)
    {
        db.AdminAuditLogs.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task<DatabasePage<AdminAuditLog>> ListAsync(long? adminId, string? action, string? targetType, string? targetId,
        DateTime? from, DateTime? to, int pageIndex, int pageSize, CancellationToken ct, bool importantOnly = false)
    {
        var query = db.AdminAuditLogs.AsNoTracking();
        if (importantOnly)
        {
            // Explicit write actions: read operations must not consume the displayed page.
            string[] actions = [
                "member.ban", "member.unban",
                "category.create", "category.update", "category.delete",
                "post.approve", "post.reject", "post.remove", "ai_flag.keep", "ai_flag.remove",
                "report.dismiss", "report.keep", "report.remove",
                "comment_report.dismiss", "comment_report.keep", "comment_report.remove", "moderation.settings.update"
            ];
            query = query.Where(a => a.TargetId != null && a.TargetId != "" && actions.Contains(a.Action));
        }
        if (adminId.HasValue) query = query.Where(a => a.AdminId == adminId);
        if (action is not null) query = query.Where(a => a.Action == action);
        if (targetType is not null) query = query.Where(a => a.TargetType == targetType);
        if (targetId is not null) query = query.Where(a => a.TargetId == targetId);
        if (from.HasValue) query = query.Where(a => a.CreatedAt >= from);
        if (to.HasValue) query = query.Where(a => a.CreatedAt <= to);
        var count = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(count, items);
    }

    public Task<AdminAuditLog?> FindAsync(long id, CancellationToken ct) =>
        db.AdminAuditLogs.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyDictionary<long, AuditDisplayContext>> GetDisplayContextsAsync(
        IReadOnlyList<AdminAuditLog> entries, CancellationToken ct)
    {
        if (entries.Count == 0) return new Dictionary<long, AuditDisplayContext>();
        static long Target(AdminAuditLog entry) => long.TryParse(entry.TargetId, out var id) ? id : 0;
        // Report actions store a report/flag ID, including comment_report whose TargetType is "comment".
        static bool IsReport(AdminAuditLog entry) => entry.Action.StartsWith("comment_report.", StringComparison.Ordinal)
            || entry.Action.StartsWith("report.", StringComparison.Ordinal) || entry.Action.StartsWith("ai_flag.", StringComparison.Ordinal);
        var flagIds = entries.Where(IsReport).Select(Target).Distinct().ToArray();
        var flags = await db.Flags.AsNoTracking().Where(f => flagIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, ct);
        var commentIds = flags.Values.Where(f => f.CommentId.HasValue).Select(f => f.CommentId!.Value).Distinct().ToArray();
        var comments = await db.Comments.AsNoTracking().Where(c => commentIds.Contains(c.Id))
            .Select(c => new { c.Id, c.UserId, c.PostId }).ToDictionaryAsync(c => c.Id, ct);
        var postIds = entries.Where(e => !IsReport(e) && e.TargetType == "post").Select(Target)
            .Concat(flags.Values.Where(f => f.PostId.HasValue).Select(f => f.PostId!.Value))
            .Concat(comments.Values.Select(c => c.PostId)).Distinct().ToArray();
        var posts = await db.Posts.AsNoTracking().Where(p => postIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Title, p.AuthorId }).ToDictionaryAsync(p => p.Id, ct);
        var categoryIds = entries.Where(e => e.TargetType == "category").Select(Target).ToArray();
        var categories = await db.Categories.AsNoTracking().Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => (long)c.Id, c => c.Name, ct);
        var userIds = entries.Select(e => e.AdminId)
            .Concat(entries.Where(e => e.TargetType is "user" or "member").Select(Target))
            .Concat(posts.Values.Select(p => p.AuthorId)).Concat(comments.Values.Select(c => c.UserId)).Distinct().ToArray();
        var users = await (from user in db.Users.AsNoTracking()
                           join profile in db.UserProfiles.AsNoTracking() on user.Id equals profile.UserId into profiles
                           from profile in profiles.DefaultIfEmpty()
                           where userIds.Contains(user.Id)
                           select new { user.Id, user.Username, Name = profile == null ? null : profile.DisplayName })
            .ToDictionaryAsync(u => u.Id, ct);
        var result = new Dictionary<long, AuditDisplayContext>();
        foreach (var entry in entries)
        {
            users.TryGetValue(entry.AdminId, out var admin);
            var target = Target(entry);
            long? postId = null, ownerId = null;
            string? name = null;
            var type = entry.TargetType;
            if (IsReport(entry))
            {
                type = entry.Action.StartsWith("comment_report.", StringComparison.Ordinal) ? "comment" : "post";
                if (flags.TryGetValue(target, out var flag))
                {
                    postId = flag.PostId;
                    if (flag.CommentId.HasValue && comments.TryGetValue(flag.CommentId.Value, out var comment))
                    {
                        ownerId = comment.UserId;
                        postId = comment.PostId;
                    }
                }
            }
            else if (type == "post") postId = target;
            else if (type is "user" or "member")
            {
                type = "user";
                if (users.TryGetValue(target, out var user)) name = string.IsNullOrWhiteSpace(user.Name) ? user.Username : user.Name;
                ownerId = target;
            }
            else if (type == "category") categories.TryGetValue(target, out name);
            string? postTitle = null;
            if (postId.HasValue && posts.TryGetValue(postId.Value, out var post))
            {
                postTitle = post.Title;
                if (type == "post") { name = post.Title; ownerId = post.AuthorId; }
            }
            var owner = ownerId.HasValue ? users.GetValueOrDefault(ownerId.Value) : null;
            result[entry.Id] = new(admin?.Name, admin?.Username, type, name, owner?.Name, owner?.Username, postTitle);
        }
        return result;
    }
}
