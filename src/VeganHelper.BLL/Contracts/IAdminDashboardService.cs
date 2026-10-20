using VeganHelper.BLL.DTOs.Admin;

namespace VeganHelper.BLL.Contracts;

public interface IAdminDashboardService
{
    Task<DashboardOverviewDto> OverviewAsync(AdminActor actor, DashboardRangeRequest request, CancellationToken ct);
    Task<DashboardActivityDto> ActivityAsync(AdminActor actor, DashboardActivityRequest request, CancellationToken ct);
    Task<DashboardCategoriesDto> CategoriesAsync(AdminActor actor, DashboardCategoriesRequest request, CancellationToken ct);
}
