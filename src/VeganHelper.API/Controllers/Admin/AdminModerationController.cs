using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeganHelper.API.Authorization;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;

namespace VeganHelper.API.Controllers.Admin;

[ApiController, Route("api/admin/moderation"), Authorize(Policy = AdminPolicies.AdminOnly)]
public sealed class AdminModerationController(IAdminModerationService service) : ControllerBase
{
    private AdminActor Actor => new(long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!, CultureInfo.InvariantCulture),
        HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier);
    [HttpGet("posts")]
    public Task<PagedResult<ModerationPostDto>> List([FromQuery] ModerationPostListRequest request, CancellationToken ct) => service.ListAsync(Actor, request, ct);
    [HttpGet("posts/{id:long:min(1)}")]
    public Task<ModerationPostDetailDto> Get(long id, CancellationToken ct) => service.GetAsync(Actor, id, ct);
    [HttpPost("posts/{id:long:min(1)}/decision")]
    public Task<ModerationDecisionDto> Decide(long id, ModerationDecisionRequest request, CancellationToken ct) => service.DecideAsync(Actor, id, request, ct);
    [HttpGet("ai-flags")]
    public Task<PagedResult<ModerationFlagDto>> Flags([FromQuery] ModerationFlagListRequest request, CancellationToken ct) => service.FlagsAsync(Actor, request, ct);
    [HttpPost("ai-flags/{id:long:min(1)}/decision")]
    public Task<ModerationDecisionDto> DecideFlag(long id, ModerationDecisionRequest request, CancellationToken ct) => service.DecideFlagAsync(Actor, id, request, ct);
    [HttpGet("settings")]
    public Task<ModerationSettingsDto> Settings(CancellationToken ct) => service.SettingsAsync(Actor, ct);
    [HttpPut("settings")]
    public Task<ModerationSettingsDto> Settings(ModerationSettingsRequest request, CancellationToken ct) => service.UpdateSettingsAsync(Actor, request, ct);
}
