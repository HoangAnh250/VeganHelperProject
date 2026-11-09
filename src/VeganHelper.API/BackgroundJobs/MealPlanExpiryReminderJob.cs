using Hangfire;
using VeganHelper.BLL.Services;

namespace VeganHelper.API.BackgroundJobs;

public sealed class MealPlanExpiryReminderJob(MealPlanExpiryReminder reminder)
{
    [Queue("meal-planning")]
    [DisableConcurrentExecution(600)]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 600 }, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task RunAsync(CancellationToken ct) => reminder.RunAsync(ct);
}
