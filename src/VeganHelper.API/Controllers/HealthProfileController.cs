using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VeganHelper.BLL.DTOs.HealthProfile;
using VeganHelper.BLL.Services;

namespace VeganHelper.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class HealthProfileController : ControllerBase
{
    private readonly IHealthProfileService _healthProfileService;

    public HealthProfileController(IHealthProfileService healthProfileService)
    {
        _healthProfileService = healthProfileService;
    }

    private long GetUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(userIdStr, out var userId) ? userId : 0;
    }

    [HttpGet]
    public async Task<IActionResult> GetHealthProfile()
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

        var profile = await _healthProfileService.GetHealthProfileAsync(userId);
        return Ok(profile);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateHealthProfile([FromBody] UpdateHealthProfileRequest request)
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

        if (request.HeightCm <= 0 || request.WeightKg <= 0)
        {
            return BadRequest("Height and Weight must be greater than 0");
        }

        var response = await _healthProfileService.UpdateHealthProfileAsync(userId, request);
        return Ok(response);
    }
}
