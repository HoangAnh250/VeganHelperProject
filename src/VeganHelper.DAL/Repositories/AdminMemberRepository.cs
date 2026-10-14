using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class AdminMemberRepository(AppDbContext db) : IAdminMemberRepository
{
    private IQueryable<AdminMemberProjection> Query(DateTime now) =>
        from user in db.Users.AsNoTracking()
        join role in db.Roles on user.RoleId equals role.Id
        join profile in db.UserProfiles on user.Id equals profile.UserId into profiles
        from profile in profiles.DefaultIfEmpty()
        let ban = db.UserBans.Where(b => b.UserId == user.Id && b.UnbannedAt == null && (b.ExpiresAt == null || b.ExpiresAt > now))
            .OrderByDescending(b => b.Id).FirstOrDefault()
        select new AdminMemberProjection
        {
            Id = user.Id, Username = user.Username, Email = user.Email, DisplayName = profile == null ? null : profile.DisplayName,
            AvatarUrl = profile == null ? null : profile.AvatarUrl, Role = role.RoleName, IsActive = user.IsActive,
            EmailVerifiedAt = user.EmailVerifiedAt, CreatedAt = user.CreatedAt, LastLoginAt = user.LastLoginAt, LockedUntil = user.LockedUntil,
            Status = user.DeletedAt != null ? "deleted" : ban != null ? "banned" : user.EmailVerifiedAt == null ? "pending_verification"
                : !user.IsActive ? "inactive" : user.LockedUntil > now ? "locked" : "active",
            BanId = ban == null ? null : ban.Id, BanReason = ban == null ? null : ban.Reason, BanExpiresAt = ban == null ? null : ban.ExpiresAt
        };

    public async Task<DatabasePage<AdminMemberProjection>> ListAsync(string? keyword, string? role, string? status, DateTime now, int pageIndex, int pageSize, CancellationToken ct)
    {
        var query = Query(now);
        if (keyword is not null)
        {
            var pattern = "%" + keyword.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            // Explicit deterministic collation: account uniqueness uses nondeterministic ICU, which cannot run ILIKE on PG17.
            query = query.Where(u => EF.Functions.ILike(EF.Functions.Collate(u.Username, "default"), pattern, "\\")
                || EF.Functions.ILike(EF.Functions.Collate(u.Email, "default"), pattern, "\\")
                || (u.DisplayName != null && EF.Functions.ILike(EF.Functions.Collate(u.DisplayName, "default"), pattern, "\\")));
        }
        if (role is not null) query = query.Where(u => u.Role == role);
        if (status is not null) query = query.Where(u => u.Status == status);
        var count = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(count, items);
    }
    public Task<AdminMemberProjection?> FindAsync(long id, DateTime now, CancellationToken ct) => Query(now).SingleOrDefaultAsync(u => u.Id == id, ct);
    public Task<User?> LockUserAsync(long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Member changes require an audited transaction.");
        return db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
    }
    public Task<UserBan?> FindActiveBanAsync(long id, DateTime now, CancellationToken ct) =>
        db.UserBans.Where(b => b.UserId == id && b.UnbannedAt == null && (b.ExpiresAt == null || b.ExpiresAt > now))
            .OrderByDescending(b => b.Id).FirstOrDefaultAsync(ct);
    public void AddBan(UserBan ban) => db.UserBans.Add(ban);
    public async Task SaveAndRevokeRefreshAsync(long userId, DateTime now, CancellationToken ct)
    {
        await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
        await db.SaveChangesAsync(ct);
    }
}
