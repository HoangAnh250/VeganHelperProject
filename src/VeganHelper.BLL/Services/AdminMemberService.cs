using System.Globalization;
using AutoMapper;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;
using VeganHelper.BLL.Exceptions;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class AdminMemberService(IAdminMemberRepository repository, IAuthRepository auth,
    IAdminAuditService audit, IMapper mapper, TimeProvider clock) : IAdminMemberService
{
    public async Task<PagedResult<AdminMemberDto>> ListAsync(AdminActor actor, AdminMemberListRequest request, CancellationToken ct)
    {
        Pagination.Validate(request.PageIndex, request.PageSize);
        if (request.Keyword?.Length > 100) throw new ArgumentException("Keyword must be at most 100 characters.");
        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim();
        var role = Normalize(request.Role);
        var status = Normalize(request.Status);
        if (role is not (null or "member" or "admin")) throw new ArgumentException("Invalid role filter.");
        if (status is not (null or "active" or "inactive" or "pending_verification" or "locked" or "banned" or "deleted"))
            throw new ArgumentException("Invalid status filter.");
        return await audit.ExecuteAsync(actor, "member.list", "user", null, async token =>
            Pagination.Map<AdminMemberProjection, AdminMemberDto>(await repository.ListAsync(keyword, role, status,
                clock.GetUtcNow().UtcDateTime, request.PageIndex, request.PageSize, token), request.PageIndex, request.PageSize, mapper), null, ct);
    }
    public Task<AdminMemberDto> GetAsync(AdminActor actor, long id, CancellationToken ct) =>
        audit.ExecuteAsync(actor, "member.view", "user", Target(id), token => DetailAsync(id, token), null, ct);
    public Task<AdminMemberDto> BanAsync(AdminActor actor, long id, BanMemberRequest request, CancellationToken ct)
    {
        var reason = Reason(request.Reason);
        return audit.ExecuteAsync(actor, "member.ban", "user", Target(id), async token =>
        {
            var user = await TargetAsync(actor, id, token);
            var now = clock.GetUtcNow().UtcDateTime; // Evaluate expiry after acquiring the row lock.
            if (request.ExpiresAt?.UtcDateTime <= now) throw new ArgumentException("Ban expiry must be in the future.");
            if (await repository.FindActiveBanAsync(id, now, token) is not null) throw new ConflictException("Member is already banned.");
            repository.AddBan(new UserBan { UserId = id, BannedByAdminId = actor.UserId, Reason = reason,
                CreatedAt = now, ExpiresAt = request.ExpiresAt?.UtcDateTime });
            user.TokenVersion = checked(user.TokenVersion + 1);
            user.UpdatedAt = now;
            await repository.SaveAndRevokeRefreshAsync(id, now, token);
            return await DetailAsync(id, token);
        }, null, ct);
    }
    public Task<AdminMemberDto> UnbanAsync(AdminActor actor, long id, UnbanMemberRequest request, CancellationToken ct)
    {
        var reason = Reason(request.Reason);
        return audit.ExecuteAsync(actor, "member.unban", "user", Target(id), async token =>
        {
            var user = await TargetAsync(actor, id, token);
            var now = clock.GetUtcNow().UtcDateTime;
            var ban = await repository.FindActiveBanAsync(id, now, token) ?? throw new ConflictException("Member has no active ban.");
            ban.UnbannedAt = now; ban.UnbannedByAdminId = actor.UserId; ban.UnbanReason = reason;
            user.TokenVersion = checked(user.TokenVersion + 1);
            user.UpdatedAt = now;
            await repository.SaveAndRevokeRefreshAsync(id, now, token);
            return await DetailAsync(id, token);
        }, null, ct);
    }
    private async Task<User> TargetAsync(AdminActor actor, long id, CancellationToken ct)
    {
        var user = await repository.LockUserAsync(id, ct);
        if (user is null || user.DeletedAt is not null) throw new NotFoundException("Member not found.");
        if (actor.UserId == id || await auth.FindRoleNameAsync(user.RoleId, ct) != "member")
            throw new UnauthorizedAccessException("Only member accounts may be banned or unbanned.");
        return user;
    }
    private async Task<AdminMemberDto> DetailAsync(long id, CancellationToken ct) => mapper.Map<AdminMemberDto>(
        await repository.FindAsync(id, clock.GetUtcNow().UtcDateTime, ct) ?? throw new NotFoundException("Member not found."));
    private static string Target(long id) => id > 0 ? id.ToString(CultureInfo.InvariantCulture) : throw new ArgumentException("Invalid member ID.");
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    private static string Reason(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 500
        ? value.Trim() : throw new ArgumentException("Reason is required and must be at most 500 characters.");
}
