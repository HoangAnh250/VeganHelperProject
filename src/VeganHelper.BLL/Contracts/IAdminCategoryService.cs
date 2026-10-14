using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;

namespace VeganHelper.BLL.Contracts;

public interface IAdminCategoryService
{
    Task<PagedResult<AdminCategoryDto>> ListAsync(AdminActor actor, AdminCategoryListRequest request, CancellationToken ct);
    Task<AdminCategoryDto> GetAsync(AdminActor actor, int id, CancellationToken ct);
    Task<AdminCategoryDto> CreateAsync(AdminActor actor, AdminCategoryWriteRequest request, CancellationToken ct);
    Task<AdminCategoryDto> UpdateAsync(AdminActor actor, int id, AdminCategoryWriteRequest request, CancellationToken ct);
    Task DeleteAsync(AdminActor actor, int id, CancellationToken ct);
}
