using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class FollowerPostNotificationDispatcher(INotificationRepository repository)
{
    public async Task<int> DispatchBatchAsync(CancellationToken ct = default)
    {
        await using var tx = await repository.BeginFanoutTransactionAsync(ct);
        var rows = await repository.PendingFollowerPostsAsync(ct);
        // Acquire every source post lock before any recipient advisory lock to avoid lock cycles.
        if (rows.Count > 0) await repository.LockFanoutPostsAsync(rows.Select(r => r.PostId).Distinct().Order().ToArray(), ct);
        // Match recipient advisory-lock order across workers. Follow rows stay locked until commit.
        foreach (var row in rows.OrderBy(r => r.UserId).ThenBy(r => r.Id))
        {
            var notification = new Notification
            {
                UserId = row.UserId, SourceFollowId = row.FollowId, SourcePostId = row.PostId,
                EventKey = $"followed-post:{row.PostId}:{row.FollowId}", Type = "followed_post",
                Title = "New post", Message = "Someone you follow published a new post.",
                TargetUrl = $"/posts/{row.PostId}", CreatedAt = row.CreatedAt
            };
            // A hide/reapproval transaction may have removed this row while we waited for its post.
            if (await repository.FollowerPostPendingAsync(row.Id, ct) &&
                await repository.NewPostPushAllowedAsync(notification, ct))
                await repository.PublishAsync(notification, ct);
            await repository.RemoveFollowerPostAsync(row.Id, ct);
        }
        await tx.CommitAsync(ct);
        return rows.Count;
    }
}
