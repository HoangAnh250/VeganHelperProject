using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeganHelper.API.Authorization;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;

namespace VeganHelper.API.Controllers.Admin;

[ApiController, Authorize(Policy = AdminPolicies.AdminOnly), Route("api/admin/moderation/comment-reports")]
public sealed class AdminCommentReportsController(IAdminCommentReportService service) : ControllerBase
{
    private AdminActor Actor => new(long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!, CultureInfo.InvariantCulture),
        HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier);

    [HttpGet]
    public Task<PagedResult<AdminCommentReportDto>> List([FromQuery] AdminCommentReportListRequest request, CancellationToken ct) =>
        service.ListAsync(Actor, request, ct);

    [HttpGet("{id:long:min(1)}")]
    public Task<AdminCommentReportDto> Get(long id, CancellationToken ct) => service.GetAsync(Actor, id, ct);

    [HttpPost("{id:long:min(1)}/decision")]
    public Task<AdminCommentReportDto> Decide(long id, AdminCommentReportDecisionRequest request, CancellationToken ct) =>
        service.DecideAsync(Actor, id, request, ct);
}
