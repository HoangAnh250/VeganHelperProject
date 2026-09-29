using Microsoft.AspNetCore.Mvc;
using VeganHelper.BLL.DTOs.Shops;
using VeganHelper.BLL.Services;

namespace VeganHelper.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ShopsController : ControllerBase
{
    private readonly IShopService _shopService;

    public ShopsController(IShopService shopService)
    {
        _shopService = shopService;
    }

    [HttpGet("nearby")]
    public async Task<IActionResult> GetNearbyShops([FromQuery] GetNearbyShopsRequest request)
    {
        var result = await _shopService.GetNearbyShopsAsync(request);
        return Ok(new { statusCode = 200, shops = result });
    }
}
