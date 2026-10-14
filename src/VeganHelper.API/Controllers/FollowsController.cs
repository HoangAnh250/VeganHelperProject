using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Follows;

namespace VeganHelper.API.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class FollowsController(IFollowService service) : ControllerBase
{
    [HttpPost("{userId:long:min(1)}/follow")]
    [ProducesResponseType(typeof(FollowStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Follow(long userId, CancellationToken cancellationToken)
    {
        if (GetUserId() is not long currentUserId) return Unauthorized();
        return Ok(await service.FollowAsync(currentUserId, userId, cancellationToken));
    }

    [HttpDelete("{userId:long:min(1)}/follow")]
    [ProducesResponseType(typeof(FollowStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unfollow(long userId, CancellationToken cancellationToken)
    {
        if (GetUserId() is not long currentUserId) return Unauthorized();
        return Ok(await service.UnfollowAsync(currentUserId, userId, cancellationToken));
    }

    [HttpPatch("{userId:long:min(1)}/follow/notifications")]
    [ProducesResponseType(typeof(FollowStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetNotifications(long userId, [FromBody] UpdateFollowNotificationsRequest request, CancellationToken cancellationToken)
    {
        if (GetUserId() is not long currentUserId) return Unauthorized();
        return Ok(await service.SetNewPostNotificationsAsync(currentUserId, userId, request.NotifyOnNewPost, cancellationToken));
    }

    [HttpGet("{userId:long:min(1)}/follow-status")]
    [ProducesResponseType(typeof(FollowStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(long userId, CancellationToken cancellationToken)
    {
        if (GetUserId() is not long currentUserId) return Unauthorized();
        return Ok(await service.GetStatusAsync(currentUserId, userId, cancellationToken));
    }

    [HttpGet("{userId:long:min(1)}/followers")]
    [ProducesResponseType(typeof(PagedResult<FollowUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFollowers(
        long userId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (GetUserId() is null) return Unauthorized();
        return Ok(await service.GetFollowersAsync(userId, pageIndex, pageSize, cancellationToken));
    }

    [HttpGet("{userId:long:min(1)}/following")]
    [ProducesResponseType(typeof(PagedResult<FollowUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFollowing(
        long userId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (GetUserId() is null) return Unauthorized();
        return Ok(await service.GetFollowingAsync(userId, pageIndex, pageSize, cancellationToken));
    }

    private long? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(value, out var id) && id > 0 ? id : null;
    }
}
