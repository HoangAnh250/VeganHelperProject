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
                "post.approve", "post.reject", "ai_flag.keep", "ai_flag.remove",
                "report.dismiss", "report.keep", "report.remove",
                "comment_report.keep", "comment_report.remove", "moderation.settings.update"
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
}
