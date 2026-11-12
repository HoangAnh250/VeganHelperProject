using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;
using VeganHelper.API.Configuration;
using VeganHelper.BLL.Contracts;

namespace VeganHelper.API.Controllers;

[ApiController]
[Authorize]
[Route("api/posts/{postId:long:min(1)}/summary")]
public sealed class PostSummariesController(IPostSummaryService service,
    IOptions<GroqSummaryOptions> options) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(PostSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(long postId, CancellationToken ct)
    {
        var result = await service.GetAsync(postId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [EnableRateLimiting("video-summary")]
    [ProducesResponseType(typeof(PostSummaryDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestSummary(long postId, CancellationToken ct)
    {
        if (!options.Value.Enabled)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Video summarization is currently unavailable." });
        var result = await service.RequestAsync(postId, ct);
        return result is null ? NotFound() : Accepted(result);
    }
}
