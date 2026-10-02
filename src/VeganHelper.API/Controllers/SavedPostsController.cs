using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Posts;
using VeganHelper.BLL.Services;

namespace VeganHelper.API.Controllers;

[ApiController]
[Authorize]
[Route("api/users/me/saved-posts")]
public sealed class SavedPostsController(IPostInteractionService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SavedPostItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSavedPosts(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await service.GetSavedPostsAsync(
            userId.Value,
            pageIndex,
            pageSize,
            cancellationToken);

        return Ok(result);
    }

    private long? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(value, out var id) ? id : null;
    }
}
