using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeganHelper.BLL.DTOs.MealPlanning;
using VeganHelper.BLL.Services.MealPlanning;

namespace VeganHelper.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public sealed class MealPlansController(IMealPlanningService service) : ControllerBase
{
    private long? UserId=>long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"),out var id)&&id>0?id:null;

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateMealPlanRequest request,CancellationToken ct)
    {
        if(UserId is not long userId) return Unauthorized();
        var result=await service.GenerateAsync(userId,request,ct);
        return CreatedAtAction(nameof(Get),new { id=result.Id },result);
    }

    [HttpGet("{id:long:min(1)}")]
    public async Task<IActionResult> Get(long id,CancellationToken ct)
    {
        if(UserId is not long userId) return Unauthorized();
        return Ok(await service.GetAsync(userId,id,ct));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery]int pageIndex=1,[FromQuery]int pageSize=10,CancellationToken ct=default)
    {
        if(UserId is not long userId) return Unauthorized();
        return Ok(await service.ListAsync(userId,pageIndex,pageSize,ct));
    }
}
