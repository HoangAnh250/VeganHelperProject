using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs.Notifications;
using VeganHelper.DAL.Integrations;

namespace VeganHelper.API.Controllers;

[ApiController, Authorize, Route("api/notifications")]
public sealed class NotificationsController(INotificationService service, IOptions<WebPushSettings> options) : ControllerBase
{
    private long? UserId => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) && id > 0 ? id : null;
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] NotificationListRequest request, CancellationToken ct)
    {
        if (UserId is not long userId) return Unauthorized();
        return Ok(await service.ListAsync(userId, request, ct));
    }
    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        if (UserId is not long userId) return Unauthorized();
        return Ok(await service.UnreadCountAsync(userId, ct));
    }
    [HttpPatch("{id:long:min(1)}/read")]
    public async Task<IActionResult> Read(long id, CancellationToken ct)
    {
        if (UserId is not long userId) return Unauthorized();
        return Ok(await service.MarkReadAsync(userId, id, ct));
    }
    [HttpPatch("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        if (UserId is not long userId) return Unauthorized();
        return Ok(await service.MarkAllReadAsync(userId, ct));
    }
    [HttpGet("push/config")]
    public IActionResult PushConfig() => Ok(new PushConfigurationResponse(options.Value.Enabled, options.Value.Enabled ? options.Value.PublicKey : null));
    [HttpPost("push/subscriptions")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionRequest request, CancellationToken ct)
    {
        if (UserId is not long userId) return Unauthorized();
        if (!options.Value.Enabled) return Conflict(new { message = "Web push is not configured. In-app notifications are available." });
        var result = await service.SubscribeAsync(userId, request, ct);
        return Created($"/api/notifications/push/subscriptions/{result.Id}", result);
    }
    [HttpDelete("push/subscriptions/{id:long:min(1)}")]
    public async Task<IActionResult> Unsubscribe(long id, CancellationToken ct)
    {
        if (UserId is not long userId) return Unauthorized();
        if (!await service.UnsubscribeAsync(userId, id, ct)) return NotFound(new { message = "Subscription not found." });
        return NoContent();
    }
}
