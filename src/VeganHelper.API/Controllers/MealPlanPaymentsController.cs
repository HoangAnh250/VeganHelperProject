using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs.MealPlanning;

namespace VeganHelper.API.Controllers;

[ApiController]
[Route("api/meal-plan-payments")]
public sealed class MealPlanPaymentsController(IMealPlanPaymentService service) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("packages")]
    [ProducesResponseType(typeof(IReadOnlyList<MealPlanPackageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPackages(CancellationToken ct) => Ok(await service.GetPackagesAsync(ct));

    [Authorize]
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(MealPlanCheckoutDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateCheckout([FromBody] CreateMealPlanCheckoutRequest request, CancellationToken ct)
    {
        if (GetUserId() is not { } userId) return Unauthorized();
        var checkout = await service.CreateCheckoutAsync(userId, request.PackageCode, ct);
        return CreatedAtAction(nameof(GetOrder), new { orderId = checkout.OrderId }, checkout);
    }

    [Authorize]
    [HttpGet("orders/{orderId:long:min(1)}")]
    [ProducesResponseType(typeof(MealPlanPaymentOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(long orderId, CancellationToken ct)
    {
        if (GetUserId() is not { } userId) return Unauthorized();
        return Ok(await service.GetOrderAsync(userId, orderId, ct));
    }

    [Authorize]
    [HttpGet("access")]
    [ProducesResponseType(typeof(MealPlanAccessDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccess(CancellationToken ct)
    {
        if (GetUserId() is not { } userId) return Unauthorized();
        return Ok(await service.GetAccessAsync(userId, ct));
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    [RequestSizeLimit(64_000)]
    [ProducesResponseType(typeof(PayOSWebhookAcknowledgement), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Webhook([FromBody] JsonElement payload, CancellationToken ct)
    {
        if (payload.ValueKind != JsonValueKind.Object)
            return BadRequest(new { error = "Invalid PayOS webhook payload.", errorCode = "INVALID_PAYOS_WEBHOOK" });

        return Ok(await service.HandleWebhookAsync(payload.GetRawText(), ct));
    }

    private long? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return long.TryParse(value, out var userId) && userId > 0 ? userId : null;
    }
}
