using Hangfire;
using VeganHelper.BLL.Services;

namespace VeganHelper.API.BackgroundJobs;

public sealed class NotificationPushJob(NotificationPushDispatcher dispatcher)
{
    [DisableConcurrentExecution(600)]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 600 }, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task RunAsync(CancellationToken ct) => dispatcher.DispatchAsync(ct);
}
