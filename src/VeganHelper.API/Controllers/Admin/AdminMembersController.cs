using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeganHelper.API.Authorization;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;

namespace VeganHelper.API.Controllers.Admin;

[ApiController, Route("api/admin/members"), Authorize(Policy = AdminPolicies.AdminOnly)]
public sealed class AdminMembersController(IAdminMemberService service) : ControllerBase
{
    private AdminActor Actor => new(long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!, CultureInfo.InvariantCulture),
        HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier);
    [HttpGet]
    public Task<PagedResult<AdminMemberDto>> List([FromQuery] AdminMemberListRequest request, CancellationToken ct) => service.ListAsync(Actor, request, ct);
    [HttpGet("{id:long:min(1)}")]
    public Task<AdminMemberDto> Get(long id, CancellationToken ct) => service.GetAsync(Actor, id, ct);
    [HttpPost("{id:long:min(1)}/ban")]
    public Task<AdminMemberDto> Ban(long id, BanMemberRequest request, CancellationToken ct) => service.BanAsync(Actor, id, request, ct);
    [HttpPost("{id:long:min(1)}/unban")]
    public Task<AdminMemberDto> Unban(long id, UnbanMemberRequest request, CancellationToken ct) => service.UnbanAsync(Actor, id, request, ct);
}
