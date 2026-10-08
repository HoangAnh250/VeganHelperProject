using AutoMapper;
using System.Net;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;
using VeganHelper.BLL.Exceptions;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class AdminAuditService(IAdminAuditRepository repository, IMapper mapper, TimeProvider clock) : IAdminAuditService
{
    public Task<bool> IsActiveAdminAsync(long userId, CancellationToken ct) =>
        repository.IsActiveAdminAsync(userId, clock.GetUtcNow().UtcDateTime, ct);

    public async Task<PagedResult<AdminAuditLogDto>> ListAsync(AdminActor actor, AdminAuditListRequest request, CancellationToken ct)
    {
        Pagination.Validate(request.PageIndex, request.PageSize);
        if (request.AdminId is <= 0 || request.From > request.To)
            throw new ArgumentException("Invalid admin ID or audit time range.");
        var action = Filter(request.Action, 100);
        var targetType = Filter(request.TargetType, 50);
        var targetId = Filter(request.TargetId, 100);
        return await ExecuteAsync(actor, "audit.list", "audit_log", null, async token =>
        {
            var page = await repository.ListAsync(request.AdminId, action, targetType, targetId,
                request.From?.UtcDateTime, request.To?.UtcDateTime, request.PageIndex, request.PageSize, token);
            return Pagination.Map<AdminAuditLog, AdminAuditLogDto>(page, request.PageIndex, request.PageSize, mapper);
        }, null, ct);
    }

    public Task<AdminAuditLogDto> GetAsync(AdminActor actor, long id, CancellationToken ct) =>
        ExecuteAsync(actor, "audit.view", "audit_log", id.ToString(System.Globalization.CultureInfo.InvariantCulture), async token =>
        {
            var entry = await repository.FindAsync(id, token) ?? throw new NotFoundException("Audit entry not found.");
            return mapper.Map<AdminAuditLogDto>(entry);
        }, null, ct);

    public async Task<T> ExecuteAsync<T>(AdminActor actor, string action, string targetType, string? targetId,
        Func<CancellationToken, Task<T>> operation, Func<T, string?>? targetIdFromResult, CancellationToken ct)
    {
        if (!await IsActiveAdminAsync(actor.UserId, ct)) throw new UnauthorizedAccessException("Administrator access is required.");
        Required(action, 100);
        Required(targetType, 50);
        Required(actor.TraceId, 100);
        if (actor.IpAddress is not null && !IPAddress.TryParse(actor.IpAddress, out _)) throw new ArgumentException("Invalid peer IP address.");
        if (actor.IpAddress?.Length > 45) throw new ArgumentException("Peer IP address is too long.");
        await using var transaction = await repository.BeginTransactionAsync(ct);
        // Disposal rolls back on any failure, including cancellation and audit persistence failure.
        var result = await operation(ct);
        var resolvedTargetId = targetIdFromResult is null ? targetId : targetIdFromResult(result);
        if (resolvedTargetId?.Length > 100) throw new ArgumentException("Audit target ID is too long.");
        await repository.AppendAsync(new AdminAuditLog
        {
            AdminId = actor.UserId, Action = action, TargetType = targetType, TargetId = resolvedTargetId,
            IpAddress = actor.IpAddress, TraceId = actor.TraceId, CreatedAt = clock.GetUtcNow().UtcDateTime
        }, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    private static string? Filter(string? value, int maxLength)
    {
        if (value?.Length > maxLength) throw new ArgumentException("Audit filter is too long.");
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
    private static void Required(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength) throw new ArgumentException("Invalid audit metadata.");
    }
}
