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

    [HttpPut("{commentId:long}")]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateComment(
        long postId,
        long commentId,
        [FromBody] UpdateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await service.UpdateCommentAsync(
            postId,
            commentId,
            userId.Value,
            request,
            cancellationToken);

        return Ok(result);
    }

    [HttpDelete("{commentId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteComment(
        long postId,
        long commentId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        await service.DeleteCommentAsync(
            postId,
            commentId,
            userId.Value,
            IsAdministrator(),
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{commentId:long}/reports")]
    [ProducesResponseType(typeof(ReportCommentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReportComment(
        long postId,
        long commentId,
        [FromBody] ReportCommentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await service.ReportCommentAsync(
            postId,
            commentId,
            userId.Value,
            request,
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    private long? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(value, out var id) ? id : null;
    }

    private bool IsAdministrator() => User.IsInRole("admin") || User.IsInRole("Admin");
}
