using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class CategoryRepository(AppDbContext db) : ICategoryRepository
{
    public Task<List<Category>> GetPostCategoriesAsync(CancellationToken cancellationToken = default) =>
        db.Categories.AsNoTracking()
            .Where(c => c.IsActive && c.CategoryType.ToLower() == "post")
            .OrderBy(c => c.Id)
            .ToListAsync(cancellationToken);
}
