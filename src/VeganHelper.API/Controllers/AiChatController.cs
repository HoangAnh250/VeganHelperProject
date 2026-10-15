using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.AI;

namespace VeganHelper.API.Controllers;

[ApiController]
[Route("api/ai")]
public sealed class AiChatController(IAiChatService service) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("chat")]
    [ProducesResponseType(typeof(AiChatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Send(
        [FromBody] AiChatRequestDto request,
        CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true && GetUserId() is not { } userId)
            return Unauthorized(new { error = "User identity is missing or invalid." });

        var result = await service.SendAsync(GetUserId(), request, cancellationToken);
        return ToActionResult(result);
    }

    [AllowAnonymous]
    [HttpGet("chat/{sessionId:long}")]
    [ProducesResponseType(typeof(AiChatHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> History(
        long sessionId,
        [FromQuery] Guid? guestSessionId,
        CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true && GetUserId() is not { } userId)
            return Unauthorized(new { error = "User identity is missing or invalid." });

        var result = await service.GetHistoryAsync(
            GetUserId(),
            sessionId,
            guestSessionId,
            cancellationToken);
        return ToActionResult(result);
    }

    private long? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(value, out var userId) ? userId : null;
    }

    private IActionResult ToActionResult<T>(ServiceResult<T> result) =>
        result.Succeeded
            ? StatusCode(result.StatusCode, result.Data)
            : StatusCode(result.StatusCode, new { error = result.Error });
}
