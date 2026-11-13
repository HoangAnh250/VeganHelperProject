using Microsoft.EntityFrameworkCore.Storage;
using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public interface IAdminAuditRepository
{
    Task<bool> IsActiveAdminAsync(long userId, DateTime now, CancellationToken ct);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);
    Task AppendAsync(AdminAuditLog entry, CancellationToken ct);
    Task<DatabasePage<AdminAuditLog>> ListAsync(long? adminId, string? action, string? targetType, string? targetId,
        DateTime? from, DateTime? to, int pageIndex, int pageSize, CancellationToken ct, bool importantOnly = false);
    Task<AdminAuditLog?> FindAsync(long id, CancellationToken ct);
    Task<IReadOnlyDictionary<long, AuditDisplayContext>> GetDisplayContextsAsync(IReadOnlyList<AdminAuditLog> entries, CancellationToken ct);
}

// Current display information, not a historical snapshot of the affected records.
public sealed record AuditDisplayContext(string? AdminName, string? AdminUsername, string SubjectType,
    string? SubjectName, string? OwnerName, string? OwnerUsername, string? PostTitle);
