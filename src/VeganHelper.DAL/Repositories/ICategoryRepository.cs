using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public interface ICategoryRepository
{
    Task<List<Category>> GetPostCategoriesAsync(CancellationToken cancellationToken = default);
}
