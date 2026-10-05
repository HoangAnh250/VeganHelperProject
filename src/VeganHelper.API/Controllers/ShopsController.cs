using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeganHelper.BLL.DTOs.Shops;
using VeganHelper.BLL.Services;

namespace VeganHelper.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class ShopsController(IShopService service) : ControllerBase
{
    [HttpGet("nearby")]
    public async Task<IActionResult> GetNearby([FromQuery] NearbyShopsRequest request, CancellationToken ct) =>
        Ok(await service.GetNearbyAsync(request, ct));

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] SearchShopsRequest request, CancellationToken ct) =>
        Ok(await service.SearchAsync(request, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetDetail(long id, [FromQuery] ShopLocationRequest request, CancellationToken ct) =>
        Ok(await service.GetDetailAsync(id, request, ct));
}
