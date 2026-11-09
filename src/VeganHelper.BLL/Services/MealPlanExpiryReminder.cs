using System.Globalization;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class MealPlanExpiryReminder(
    IMealPlanPaymentRepository payments, INotificationRepository notifications, TimeProvider clock)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        long afterUserId = 0;
        const int batchSize = 100;
        while (true)
        {
            var batch = await payments.GetExpiringAccessAsync(now, now.AddDays(7), afterUserId, batchSize, ct);
            foreach (var access in batch)
            {
                ct.ThrowIfCancellationRequested();
                var expiry = access.ExpiresAt.ToString("dd MMM yyyy 'at' HH:mm 'UTC'", CultureInfo.InvariantCulture);
                await notifications.PublishAsync(new Notification
                {
                    UserId = access.UserId,
                    EventKey = $"meal-package-expiry:{access.ExpiresAt.Ticks}",
                    // Reuse the existing weekly-menu notification category and browser push outbox.
                    Type = "meal_reminder",
                    Title = "Your Weekly Menu package expires soon",
                    Message = $"Your package expires on {expiry}. Please renew to continue creating weekly menus. If your package expires during a weekly menu, you can still finish that week, but you will need to renew before creating the next one.",
                    TargetUrl = "/weekly-menu",
                    CreatedAt = now
                }, ct);
            }
            if (batch.Count < batchSize) return;
            afterUserId = batch[^1].UserId;
        }
    }
}
