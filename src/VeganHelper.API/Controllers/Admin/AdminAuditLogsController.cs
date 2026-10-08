using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeganHelper.API.Authorization;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;

namespace VeganHelper.API.Controllers.Admin;

[ApiController, Route("api/admin/audit-logs"), Authorize(Policy = AdminPolicies.AdminOnly)]
public sealed class AdminAuditLogsController(IAdminAuditService service) : ControllerBase
{
    private AdminActor Actor => new(
        long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!, CultureInfo.InvariantCulture),
        HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier);

    [HttpGet]
    public Task<PagedResult<AdminAuditLogDto>> List([FromQuery] AdminAuditListRequest request, CancellationToken ct) =>
        service.ListAsync(Actor, request, ct);

    [HttpGet("{id:long:min(1)}")]
    public Task<AdminAuditLogDto> Get(long id, CancellationToken ct) => service.GetAsync(Actor, id, ct);
}
