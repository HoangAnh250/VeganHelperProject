using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;

namespace VeganHelper.BLL.Contracts;

public interface IAdminAuditService
{
    Task<bool> IsActiveAdminAsync(long userId, CancellationToken ct);
    Task<PagedResult<AdminAuditLogDto>> ListAsync(AdminActor actor, AdminAuditListRequest request, CancellationToken ct);
    Task<AdminAuditLogDto> GetAsync(AdminActor actor, long id, CancellationToken ct);
    Task<T> ExecuteAsync<T>(AdminActor actor, string action, string targetType, string? targetId,
        Func<CancellationToken, Task<T>> operation, Func<T, string?>? targetIdFromResult, CancellationToken ct);
}
