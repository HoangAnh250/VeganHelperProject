using Hangfire;
namespace VeganHelper.API.BackgroundJobs;

public sealed class RecurringJobRegistrationWorker(IServiceProvider services,
    ILogger<RecurringJobRegistrationWorker> logger, bool push, bool meal, bool moderation) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!push && !meal && !moderation) return;
        for (var attempt = 1; attempt <= 5 && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var jobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
                if (push) jobs.AddOrUpdate<NotificationPushJob>("notifications-web-push", j => j.RunAsync(CancellationToken.None), Cron.Minutely());
                if (meal)
                {
                    jobs.AddOrUpdate<MealRoutineJob>("meal-routine-review", j => j.RunAsync(CancellationToken.None), Cron.Minutely());
                    jobs.AddOrUpdate<MealPlanExpiryReminderJob>("meal-package-expiry-reminder", j => j.RunAsync(CancellationToken.None), Cron.Hourly());
                }
                if (moderation) jobs.AddOrUpdate<PostModerationJob>("posts-ai-moderation", j => j.RunAsync(CancellationToken.None), Cron.Minutely());
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Recurring job registration failed (attempt {Attempt}/5). API remains available.", attempt);
            }
            if (attempt < 5) await Task.Delay(TimeSpan.FromSeconds(attempt * 10), stoppingToken);
        }
    }
}
