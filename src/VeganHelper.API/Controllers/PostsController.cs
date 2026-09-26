namespace VeganHelper.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using VeganHelper.BLL.DTOs.Posts;
using VeganHelper.BLL.Services;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Requires the user to be logged in
public class PostsController : ControllerBase
{
    private readonly IPostService _postService;

    public PostsController(IPostService postService)
    {
        _postService = postService;
    }

    /// <summary>
    /// Creates a new post (article, video, recipe).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreatePost([FromForm] CreatePostRequest request, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized(new { message = "User identity is missing or invalid." });
        }

        var postId = await _postService.CreatePostAsync(request, userId, cancellationToken);

        return CreatedAtAction(nameof(CreatePost), new { id = postId }, new { id = postId, message = "Post created successfully and is pending review." });
    }

    /// <summary>
    /// Gets a paginated list of posts created by the current user.
    /// </summary>
    [HttpGet("my-posts")]
    [ProducesResponseType(typeof(PagedResult<MyPostItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyPosts([FromQuery] GetMyPostsRequest request, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized(new { message = "User identity is missing or invalid." });
        }

        var result = await _postService.GetMyPostsAsync(userId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets the details of a specific post by its ID.
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PostDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPostDetail(long id, CancellationToken cancellationToken)
    {
        var result = await _postService.GetPostDetailAsync(id, cancellationToken);
        return Ok(result);
    }
}
