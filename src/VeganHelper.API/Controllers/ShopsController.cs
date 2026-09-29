using Microsoft.AspNetCore.Mvc;
using VeganHelper.BLL.DTOs.Shops;
using VeganHelper.BLL.Services;

namespace VeganHelper.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShopsController : ControllerBase
{
    private readonly IShopService _shopService;

    public ShopsController(IShopService shopService)
    {
        _shopService = shopService;
    }

    [HttpGet("search")]
    public async Task<ActionResult<SearchShopsResponse>> SearchShops(
        [FromQuery] string? keyword, 
        [FromQuery] decimal? lat, 
        [FromQuery] decimal? lng,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10)
    {
        var response = await _shopService.SearchShopsAsync(keyword ?? string.Empty, lat, lng, pageIndex, pageSize);
        return Ok(response);
    }
}
