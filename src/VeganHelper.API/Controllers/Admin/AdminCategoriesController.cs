using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeganHelper.API.Authorization;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;

namespace VeganHelper.API.Controllers.Admin;

[ApiController, Route("api/admin/categories"), Authorize(Policy = AdminPolicies.AdminOnly)]
public sealed class AdminCategoriesController(IAdminCategoryService service) : ControllerBase
{
    private AdminActor Actor => new(long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!, CultureInfo.InvariantCulture),
        HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.TraceIdentifier);
    [HttpGet]
    public Task<PagedResult<AdminCategoryDto>> List([FromQuery] AdminCategoryListRequest request, CancellationToken ct) => service.ListAsync(Actor, request, ct);
    [HttpGet("{id:int:min(1)}")]
    public Task<AdminCategoryDto> Get(int id, CancellationToken ct) => service.GetAsync(Actor, id, ct);
    [HttpPost]
    public async Task<IActionResult> Create(AdminCategoryWriteRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(Actor, request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }
    [HttpPut("{id:int:min(1)}")]
    public Task<AdminCategoryDto> Update(int id, AdminCategoryWriteRequest request, CancellationToken ct) => service.UpdateAsync(Actor, id, request, ct);
    [HttpDelete("{id:int:min(1)}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(Actor, id, ct);
        return NoContent();
    }
}
