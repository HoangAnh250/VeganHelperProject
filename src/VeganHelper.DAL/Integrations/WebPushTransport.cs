using System.Text.Json;
using VeganHelper.DAL.Entities;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;

namespace VeganHelper.DAL.Integrations;

public sealed class WebPushSettings
{
    public bool Enabled { get; set; }
    public string Subject { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public string PrivateKey { get; set; } = "";
}
public interface IWebPushTransport
{
    Task<int> SendAsync(BrowserPushSubscription subscription, Notification notification, WebPushSettings settings, CancellationToken ct);
}
public sealed class WebPushTransport(HttpClient http) : IWebPushTransport
{
    public async Task<int> SendAsync(BrowserPushSubscription subscription, Notification notification, WebPushSettings settings, CancellationToken ct)
    {
        var client = new PushServiceClient(http);
        using var authentication = new VapidAuthentication(settings.PublicKey, settings.PrivateKey) { Subject = settings.Subject };
        // Stable tag lets a service worker replace a duplicate push after an ambiguous network failure.
        var payload = JsonSerializer.Serialize(new
        {
            notificationId = notification.Id,
            title = notification.Title,
            body = notification.Message,
            url = notification.TargetUrl,
            tag = $"notification-{notification.Id}"
        });
        var target = new PushSubscription
        {
            Endpoint = subscription.Endpoint,
            Keys = new Dictionary<string, string> { ["p256dh"] = subscription.P256dh, ["auth"] = subscription.Auth }
        };
        try
        {
            await client.RequestPushMessageDeliveryAsync(target, new PushMessage(payload) { TimeToLive = 3600 },
             authentication, ct);
            return 201;
        }
        catch (PushServiceClientException error) { return (int)error.StatusCode; }
    }
}
