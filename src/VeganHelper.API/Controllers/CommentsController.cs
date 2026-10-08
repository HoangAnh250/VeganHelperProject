using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeganHelper.BLL.DTOs.Comments;
using VeganHelper.BLL.Services;

namespace VeganHelper.API.Controllers;

[ApiController]
[Route("api/posts/{postId:long}/comments")]
[Authorize]
public sealed class CommentsController(ICommentService service) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<CommentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetComments(
        long postId,
        [FromQuery] int maxDepth = 2,
        CancellationToken cancellationToken = default)
    {
        var viewerUserId = GetUserId();
        var result = await service.GetCommentsAsync(postId, viewerUserId, maxDepth, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateComment(
        long postId,
        [FromBody] CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await service.CreateCommentAsync(postId, userId.Value, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    private long? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(value, out var id) ? id : null;
    }
}
