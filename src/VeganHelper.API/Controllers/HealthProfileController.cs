using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VeganHelper.BLL.DTOs.HealthProfile;
using VeganHelper.BLL.Services;

namespace VeganHelper.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public sealed class HealthProfileController(IHealthProfileService service) : ControllerBase
{
    private long? UserId => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) && id > 0 ? id : null;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (UserId is not long id) return Unauthorized();
        return Ok(await service.GetHealthProfileAsync(id, ct));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateHealthProfileRequest request, CancellationToken ct)
    {
        if (UserId is not long id) return Unauthorized();
        return Ok(await service.UpdateHealthProfileAsync(id, request, ct));
    }

    [HttpGet("bmi")]
    public async Task<IActionResult> GetBmi(CancellationToken ct)
    {
        if (UserId is not long id) return Unauthorized();
        return Ok(await service.GetBmiResultAsync(id, ct));
    }

    [HttpGet("bmi-history")]
    public async Task<IActionResult> GetHistory([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default)
    {
        if (UserId is not long id) return Unauthorized();
        return Ok(await service.GetBmiHistoryAsync(id, pageIndex, pageSize, ct));
    }

    [HttpPut("allergies")]
    public async Task<IActionResult> DeclareAllergies([FromBody] DeclareAllergiesRequest request, CancellationToken ct)
    {
        if (UserId is not long id) return Unauthorized();
        await service.DeclareAllergiesAsync(id, request, ct);
        return Ok(new { message = "Cập nhật danh sách dị ứng thành công" });
    }

    [HttpGet("ingredients")]
    public async Task<IActionResult> GetIngredients([FromQuery] string? keyword, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await service.GetIngredientsAsync(keyword, pageIndex, pageSize, ct));
    }

    [HttpGet("/api/posts/{postId:long}/allergy-warnings")]
    public async Task<IActionResult> GetPostWarnings(long postId, CancellationToken ct)
    {
        if (UserId is not long id) return Unauthorized();
        return Ok(await service.GetPostWarningsAsync(id, postId, ct));
    }
}
