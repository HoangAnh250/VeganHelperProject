using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeganHelper.BLL.DTOs.MealPlanning;
using VeganHelper.BLL.Services.MealPlanning;

namespace VeganHelper.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public sealed class MealPlansController(IMealPlanningService service,MealRoutineService routines) : ControllerBase
{
    private long? UserId=>long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"),out var id)&&id>0?id:null;

    [HttpPost("goal-preview")]
    public async Task<IActionResult> PreviewGoal(GenerateMealPlanRequest request,CancellationToken ct)
    {
        if(UserId is not long userId) return Unauthorized();
        return Ok(await routines.PreviewGoalAsync(userId,request,ct));
    }
    [HttpPut("{id:long:min(1)}/tracking")]
    public async Task<IActionResult> Log(long id,LogMealRequest request,CancellationToken ct)
    {
        if(UserId is not long userId) return Unauthorized();
        return Ok(await routines.LogAsync(userId,id,request,ct));
    }
    [HttpPut("{id:long:min(1)}/reminders")]
    public async Task<IActionResult> Reminders(long id,MealReminderSettingsRequest request,CancellationToken ct)
    {
        if(UserId is not long userId) return Unauthorized();
        return Ok(await routines.UpdateRemindersAsync(userId,id,request,ct));
    }
    [HttpPost("{id:long:min(1)}/revision-preview")]
    public async Task<IActionResult> PreviewRevision(long id,RevisionRequest request,CancellationToken ct)
    {
        if(UserId is not long userId) return Unauthorized();
        return Ok(await routines.PreviewRevisionAsync(userId,id,request.NextWeek,ct,request.TargetWeightKg,request.TargetDate,request.EligibilityConfirmed));
    }
    [HttpPost("{id:long:min(1)}/revisions")]
    public async Task<IActionResult> Revise(long id,RevisionRequest request,CancellationToken ct)
    {
        if(UserId is not long userId) return Unauthorized();
        var result=await routines.ConfirmRevisionAsync(userId,id,request,ct);
        return CreatedAtAction(nameof(Get),new{id=result.Id},result);
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateMealPlanRequest request,CancellationToken ct)
    {
        if(UserId is not long userId) return Unauthorized();
        var result=await service.GenerateAsync(userId,request,ct);
        return CreatedAtAction(nameof(Get),new { id=result.Id },result);
    }

    [HttpPost("{id:long:min(1)}/regenerate")]
    public async Task<IActionResult> Regenerate(long id,CancellationToken ct)
    {
        if(UserId is not long userId) return Unauthorized();
        return Ok(await service.RegenerateAsync(userId,id,ct));
    }

    [HttpDelete("{id:long:min(1)}")]
    public async Task<IActionResult> Delete(long id,CancellationToken ct)
    {
        if(UserId is not long userId) return Unauthorized();
        await service.ArchiveAsync(userId,id,ct);
        return NoContent();
    }

    [HttpGet("{id:long:min(1)}")]
    public async Task<IActionResult> Get(long id,CancellationToken ct)
    {
        if(UserId is not long userId) return Unauthorized();
        return Ok(await service.GetAsync(userId,id,ct));
    }

    [HttpPatch("{id:long:min(1)}/name")]
    public async Task<IActionResult> Rename(long id,[FromBody] UpdateMealPlanNameRequest request,CancellationToken ct)
    {
        if(UserId is not long userId) return Unauthorized();
        return Ok(await service.RenameAsync(userId,id,request,ct));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery]int pageIndex=1,[FromQuery]int pageSize=10,CancellationToken ct=default)
    {
        if(UserId is not long userId) return Unauthorized();
        return Ok(await service.ListAsync(userId,pageIndex,pageSize,ct));
    }
}
