using Microsoft.EntityFrameworkCore.Storage;
using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public interface IAdminAuditRepository
{
    Task<bool> IsActiveAdminAsync(long userId, DateTime now, CancellationToken ct);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);
    Task AppendAsync(AdminAuditLog entry, CancellationToken ct);
    Task<DatabasePage<AdminAuditLog>> ListAsync(long? adminId, string? action, string? targetType, string? targetId,
        DateTime? from, DateTime? to, int pageIndex, int pageSize, CancellationToken ct);
    Task<AdminAuditLog?> FindAsync(long id, CancellationToken ct);
}
