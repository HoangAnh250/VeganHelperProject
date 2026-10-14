using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public sealed class AdminMemberProjection
{
    public long Id { get; init; }
    public string Username { get; init; } = "";
    public string Email { get; init; } = "";
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public string Role { get; init; } = "";
    public string Status { get; init; } = "";
    public bool IsActive { get; init; }
    public DateTime? EmailVerifiedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public DateTime? LockedUntil { get; init; }
    public long? BanId { get; init; }
    public string? BanReason { get; init; }
    public DateTime? BanExpiresAt { get; init; }
}

public interface IAdminMemberRepository
{
    Task<DatabasePage<AdminMemberProjection>> ListAsync(string? keyword, string? role, string? status, DateTime now, int pageIndex, int pageSize, CancellationToken ct);
    Task<AdminMemberProjection?> FindAsync(long id, DateTime now, CancellationToken ct);
    Task<User?> LockUserAsync(long id, CancellationToken ct);
    Task<UserBan?> FindActiveBanAsync(long id, DateTime now, CancellationToken ct);
    void AddBan(UserBan ban);
    Task SaveAndRevokeRefreshAsync(long userId, DateTime now, CancellationToken ct);
}
