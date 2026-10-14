using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public sealed class AdminCategoryProjection
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Slug { get; init; } = "";
    public string CategoryType { get; init; } = "";
    public string? PostCategoryKind { get; init; }
    public bool IsActive { get; init; }
    public long PostCount { get; init; }
}

public interface IAdminCategoryRepository
{
    Task<DatabasePage<AdminCategoryProjection>> ListAsync(string? keyword, string? kind, bool? active, int pageIndex, int pageSize, CancellationToken ct);
    Task<AdminCategoryProjection?> FindAsync(int id, CancellationToken ct);
    Task AcquireWriteLockAsync(CancellationToken ct);
    Task<Category?> LockAsync(int id, CancellationToken ct);
    Task<bool> HasConflictAsync(int? exceptId, string name, string slug, CancellationToken ct);
    Task<bool> IsReferencedAsync(int id, CancellationToken ct);
    Task<int> CreateAsync(Category category, CancellationToken ct);
    Task UpdateAsync(int id, string name, string slug, string kind, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public sealed class CategoryWriteConflictException(string message, Exception inner) : Exception(message, inner);
