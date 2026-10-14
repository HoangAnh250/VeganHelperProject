using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class CategoryService(ICategoryRepository repository) : ICategoryService
{
    public async Task<List<CategoryItemDto>> GetPostCategoriesAsync(CancellationToken cancellationToken = default) =>
        (await repository.GetPostCategoriesAsync(cancellationToken)).Select(c => new CategoryItemDto
        {
            Id = c.Id, Name = c.Name, Slug = c.Slug, PostCategoryKind = c.PostCategoryKind
        }).ToList();
}
