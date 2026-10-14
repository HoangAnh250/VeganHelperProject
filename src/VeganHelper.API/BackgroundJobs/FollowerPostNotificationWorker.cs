using VeganHelper.BLL.Services;

namespace VeganHelper.API.BackgroundJobs;

// In-app fan-out must work even when browser push and Gemini are disabled.
public sealed class FollowerPostNotificationWorker(IServiceScopeFactory scopes, ILogger<FollowerPostNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<FollowerPostNotificationDispatcher>().DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                logger.LogError(error, "Follower post notification batch failed; pending work will be retried.");
            }
        }
    }
}
