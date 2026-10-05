using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VeganHelper.DAL.Integrations;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class NotificationPushDispatcher(INotificationRepository repository, IWebPushTransport transport,
 IOptions<WebPushSettings> settings, TimeProvider clock, ILogger<NotificationPushDispatcher> logger)
{
    public async Task DispatchAsync(CancellationToken ct)
    {
        if (!settings.Value.Enabled) return;
        var rows = await repository.ClaimPushAsync(clock.GetUtcNow().UtcDateTime, Guid.NewGuid(), ct);
        foreach (var delivery in rows)
        {
            ct.ThrowIfCancellationRequested();
            var state = "sent";
            string? code = null;
            var next = clock.GetUtcNow().UtcDateTime;
            if (!delivery.Subscription.IsActive || delivery.Subscription.UserId != delivery.Notification.UserId)
                state = "cancelled";
            else
            {
                try
                {
                    var status = await transport.SendAsync(delivery.Subscription, delivery.Notification, settings.Value, ct);
                    if (status is 404 or 410) { state = "expired"; code = $"http_{status}"; }
                    else if (status is < 200 or >= 300)
                    {
                        code = $"http_{status}";
                        state = status is 408 or 429 or >= 500 ? "pending" : "failed";
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
                { state = "pending"; code = "transport_error"; }
                catch (Exception) { state = "failed"; code = "push_configuration_or_payload"; }
            }
            if (state == "pending")
            {
                if (delivery.Attempts >= 5) state = "failed";
                else next = next.AddSeconds(RetryDelaySeconds(delivery.Attempts));
            }
            await repository.FinishPushAsync(delivery, state, code, next, ct);
            if (state == "failed")
                logger.LogError("Push delivery {DeliveryId} permanently failed after {Attempts} attempts; code {ErrorCode}.", delivery.Id, delivery.Attempts, code);
        }
    }
    public static int RetryDelaySeconds(int attempt) => attempt switch { <= 1 => 30, 2 => 120, 3 => 600, _ => 1800 };
}
