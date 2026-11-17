using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;
using VeganHelper.DAL.Storage;

namespace VeganHelper.API.BackgroundJobs;

public sealed class AvatarCleanupWorker(IServiceScopeFactory scopes, ILogger<AvatarCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await RemoveDueAvatarsAsync(stoppingToken); }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            { logger.LogWarning(error, "Avatar cleanup sweep could not run."); }
        }
    }

    private async Task RemoveDueAvatarsAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var due = await db.Set<AvatarCleanupItem>().Where(x => x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt).Take(20).ToListAsync(ct);
        if (due.Count == 0) return;
        var storage = scope.ServiceProvider.GetRequiredService<IAvatarStorage>();
        foreach (var item in due)
        {
            try
            {
                if (!storage.OwnsAvatar(item.Url) ||
                    await db.UserProfiles.AnyAsync(profile => profile.AvatarUrl == item.Url, ct))
                {
                    db.Remove(item);
                    continue;
                }

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                await storage.DeleteAvatarAsync(item.Url, timeout.Token);
                db.Remove(item);
            }
            catch (Exception error) when (!ct.IsCancellationRequested)
            {
                item.Attempts = Math.Min(item.Attempts + 1, 12);
                item.NextAttemptAt = now.AddMinutes(Math.Min(720, 1 << item.Attempts));
                logger.LogWarning(error, "Avatar cleanup item {CleanupId} will retry.", item.Id);
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
