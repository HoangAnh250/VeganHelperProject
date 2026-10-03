using VeganHelper.BLL.DTOs;

namespace VeganHelper.BLL.Contracts;

public interface ICategoryService
{
    Task<List<CategoryItemDto>> GetPostCategoriesAsync(CancellationToken cancellationToken = default);
}
