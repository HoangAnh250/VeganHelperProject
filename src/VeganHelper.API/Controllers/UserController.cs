using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeganHelper.BLL.Contracts.Services;
using VeganHelper.BLL.DTOs;

namespace VeganHelper.API.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UserController(IUserService service) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return ToActionResult(await service.GetProfileAsync(userId.Value, cancellationToken));
    }

    [HttpPut("me")]
    [RequestSizeLimit(5 * 1024 * 1024 + 64 * 1024)]
    public async Task<IActionResult> UpdateMe(
        [FromForm] UpdateProfileRequestDto request,
        [FromForm(Name = "avatar")] IFormFile? avatar,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (avatar is not null && (avatar.Length > 5 * 1024 * 1024 || avatar.ContentType is not ("image/jpeg" or "image/png")))
            return BadRequest(new { error = "Avatar must be a JPG or PNG image no larger than 5 MB." });

        byte[]? avatarBytes = null;
        if (avatar is not null)
        {
            await using var stream = new MemoryStream();
            await avatar.CopyToAsync(stream, cancellationToken);
            avatarBytes = stream.ToArray();
        }

        var command = new UpdateProfileCommand(
            request.DisplayName,
            request.PhoneNumber,
            request.HeightCm,
            request.WeightKg,
            request.BirthDate,
            request.BiologicalSex,
            request.DietType,
            avatarBytes,
            avatar?.FileName,
            avatar?.ContentType);
        return ToActionResult(await service.UpdateProfileAsync(userId.Value, command, cancellationToken));
    }

    private long? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(value, out var id) ? id : null;
    }

    private IActionResult ToActionResult<T>(ServiceResult<T> result) =>
        result.Succeeded ? Ok(result.Data) : StatusCode(result.StatusCode, new { error = result.Error });
}
