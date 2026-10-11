using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeganHelper.API.Authorization;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;
using VeganHelper.BLL.DTOs.Posts;

namespace VeganHelper.API.Controllers.Admin;

[ApiController, Authorize(Policy = AdminPolicies.AdminOnly), Route("api/admin/moderation/reports")]
public sealed class AdminPostReportsController(IPostReportService service) : ControllerBase
{
    private AdminActor Actor => new(long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
        HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier);
    [HttpGet] public Task<PagedResult<AdminPostReportDto>> List([FromQuery] PostReportListRequest request, CancellationToken ct) => service.ListAsync(Actor, request, ct);
    [HttpGet("{id:long:min(1)}")] public Task<AdminPostReportDetailDto> Get(long id, CancellationToken ct) => service.GetAsync(Actor, id, ct);
    [HttpPost("{id:long:min(1)}/decision")] public Task<AdminPostReportDto> Decide(long id, ModerationDecisionRequest request, CancellationToken ct) => service.DecideAsync(Actor, id, request, ct);
}
