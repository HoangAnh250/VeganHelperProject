using Microsoft.EntityFrameworkCore;
using Npgsql;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class AdminCategoryRepository(AppDbContext db) : IAdminCategoryRepository
{
    private IQueryable<AdminCategoryProjection> Query() => db.Categories.AsNoTracking().Where(c => c.CategoryType == "post")
        .Select(c => new AdminCategoryProjection
        {
            Id = c.Id, Name = c.Name, Slug = c.Slug, CategoryType = c.CategoryType, PostCategoryKind = c.PostCategoryKind,
            IsActive = c.IsActive, PostCount = db.PostCategories.LongCount(p => p.CategoryId == c.Id)
        });

    public async Task<DatabasePage<AdminCategoryProjection>> ListAsync(string? keyword, string? kind, bool? active, int pageIndex, int pageSize, CancellationToken ct)
    {
        var query = Query();
        if (keyword is not null)
        {
            var pattern = "%" + keyword.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(c => EF.Functions.ILike(EF.Functions.Collate(c.Name, "default"), pattern, "\\")
                || EF.Functions.ILike(EF.Functions.Collate(c.Slug, "default"), pattern, "\\"));
        }
        if (kind is not null) query = query.Where(c => c.PostCategoryKind == kind);
        if (active.HasValue) query = query.Where(c => c.IsActive == active.Value);
        var count = await query.LongCountAsync(ct);
        var items = await query.OrderBy(c => c.Name).ThenBy(c => c.Id).Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(count, items);
    }
    public Task<AdminCategoryProjection?> FindAsync(int id, CancellationToken ct) => Query().SingleOrDefaultAsync(c => c.Id == id, ct);

    public async Task AcquireWriteLockAsync(CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Category changes require an audited transaction.");
        // ponytail: serialize low-volume Admin category writes so case-insensitive prechecks cannot race.
        // A database unique index on normalized keys can replace this if write throughput grows.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(3912040)", ct);
    }
    public Task<Category?> LockAsync(int id, CancellationToken ct) => db.Categories
        .FromSqlInterpolated($"SELECT * FROM categories WHERE id = {id} AND category_type = 'post' FOR UPDATE")
        .AsNoTracking().SingleOrDefaultAsync(ct);
    public Task<bool> HasConflictAsync(int? exceptId, string name, string slug, CancellationToken ct) => db.Categories.AnyAsync(c =>
        (!exceptId.HasValue || c.Id != exceptId.Value) &&
        ((c.CategoryType == "post" && EF.Functions.Collate(c.Name, "veganhelper_ci") == name)
            || EF.Functions.Collate(c.Slug, "veganhelper_ci") == slug), ct);
    public async Task<bool> IsReferencedAsync(int id, CancellationToken ct) =>
        await db.PostCategories.AnyAsync(p => p.CategoryId == id, ct) || await db.ShopCategories.AnyAsync(s => s.CategoryId == id, ct);
    public async Task<int> CreateAsync(Category category, CancellationToken ct)
    {
        db.Categories.Add(category);
        await WriteAsync(() => db.SaveChangesAsync(ct));
        return category.Id;
    }
    public async Task UpdateAsync(int id, string name, string slug, string kind, CancellationToken ct)
    {
        // Name and Slug are alternate keys in the existing model; tracked key mutation is rejected by EF.
        await WriteAsync(() => db.Categories.Where(c => c.Id == id && c.CategoryType == "post").ExecuteUpdateAsync(s => s
            .SetProperty(c => c.Name, name).SetProperty(c => c.Slug, slug).SetProperty(c => c.PostCategoryKind, kind), ct));
    }
    public async Task DeleteAsync(int id, CancellationToken ct) =>
        await WriteAsync(() => db.Categories.Where(c => c.Id == id && c.CategoryType == "post").ExecuteDeleteAsync(ct));

    private static async Task<int> WriteAsync(Func<Task<int>> operation)
    {
        try { return await operation(); }
        catch (Exception ex) when ((ex is PostgresException pg ? pg : (ex as DbUpdateException)?.InnerException as PostgresException)
            is { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation,
                 ConstraintName: "UQ_categories_slug" or "UQ_categories_1" or "FK_post_categories_2" or "FK_shop_categories_2" })
        {
            throw new CategoryWriteConflictException("Category name/slug conflicts or the category is in use.", ex);
        }
    }
}
