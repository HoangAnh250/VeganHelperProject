using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeganHelper.API.Authorization;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs.Admin;

namespace VeganHelper.API.Controllers.Admin;

[ApiController, Route("api/admin/dashboard"), Authorize(Policy = AdminPolicies.AdminOnly)]
public sealed class AdminDashboardController(IAdminDashboardService service) : ControllerBase
{
    private AdminActor Actor => new(long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!, CultureInfo.InvariantCulture),
        HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier);
    [HttpGet("overview")]
    public Task<DashboardOverviewDto> Overview([FromQuery] DashboardRangeRequest request, CancellationToken ct) => service.OverviewAsync(Actor, request, ct);
    [HttpGet("post-activity")]
    public Task<DashboardActivityDto> Activity([FromQuery] DashboardActivityRequest request, CancellationToken ct) => service.ActivityAsync(Actor, request, ct);
    [HttpGet("top-categories")]
    public Task<DashboardCategoriesDto> Categories([FromQuery] DashboardCategoriesRequest request, CancellationToken ct) => service.CategoriesAsync(Actor, request, ct);
}
