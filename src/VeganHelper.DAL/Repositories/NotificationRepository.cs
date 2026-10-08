using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class NotificationRepository(AppDbContext db) : INotificationRepository
{
    public async Task<(int Count, List<Notification> Items)> ListAsync(long userId, int page, int size, bool unreadOnly, CancellationToken ct)
    {
        var query = db.Set<Notification>().AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);
        var count = await query.CountAsync(ct);
        return (count, await query.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
         .Skip(checked((page - 1) * size)).Take(size).ToListAsync(ct));
    }
    public Task<int> UnreadCountAsync(long userId, CancellationToken ct) =>
     db.Set<Notification>().CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    public async Task<(bool Exists, int Updated)> MarkReadAsync(long userId, long id, DateTime now, CancellationToken ct)
    {
        var query = db.Set<Notification>().Where(n => n.UserId == userId && n.Id == id);
        var changed = await query.Where(n => !n.IsRead).ExecuteUpdateAsync(u => u
         .SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAt, now), ct);
        return (changed > 0 || await query.AnyAsync(ct), changed);
    }
    public async Task<int> MarkAllReadAsync(long userId, DateTime now, CancellationToken ct)
    {
        // Do not consume a notification inserted after the caller's read-all boundary.
        var maximum = await db.Set<Notification>().Where(n => n.UserId == userId).MaxAsync(n => (long?)n.Id, ct) ?? 0;
        return await db.Set<Notification>().Where(n => n.UserId == userId && !n.IsRead && n.Id <= maximum)
         .ExecuteUpdateAsync(u => u.SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAt, now), ct);
    }
    public async Task<long> SubscribeAsync(BrowserPushSubscription s, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({s.UserId})", ct);
        var existing = await db.Set<BrowserPushSubscription>().AsNoTracking().FirstOrDefaultAsync(p => p.EndpointHash == s.EndpointHash, ct);
        if (existing is not null && existing.UserId != s.UserId && existing.IsActive) return 0;
        if ((existing is null || !existing.IsActive) && await db.Set<BrowserPushSubscription>().CountAsync(p => p.UserId == s.UserId && p.IsActive, ct) >= 20)
            return -1;
        var results = await db.Set<BrowserPushSubscription>().FromSqlInterpolated($"""
   INSERT INTO browser_push_subscriptions (user_id, endpoint, endpoint_hash, p256dh, auth, is_active, updated_at)
   VALUES ({s.UserId}, {s.Endpoint}, {s.EndpointHash}, {s.P256dh}, {s.Auth}, true, {s.UpdatedAt})
   ON CONFLICT (endpoint_hash) DO UPDATE SET user_id = EXCLUDED.user_id, p256dh = EXCLUDED.p256dh, auth = EXCLUDED.auth,
   is_active = true, updated_at = EXCLUDED.updated_at
   WHERE browser_push_subscriptions.user_id = EXCLUDED.user_id OR NOT browser_push_subscriptions.is_active
   RETURNING *
   """).AsNoTracking().ToListAsync(ct);
        await tx.CommitAsync(ct);
        return results.FirstOrDefault()?.Id ?? 0;
    }
    public async Task<bool> UnsubscribeAsync(long userId, long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({userId})", ct);
        var query = db.Set<BrowserPushSubscription>().Where(s => s.Id == id && s.UserId == userId);
        if (!await query.AnyAsync(ct)) return false;
        await query.ExecuteUpdateAsync(u => u.SetProperty(s => s.IsActive, false), ct);
        await db.Set<NotificationPushDelivery>().Where(d => d.SubscriptionId == id && d.State == "pending")
         .ExecuteUpdateAsync(u => u.SetProperty(d => d.State, "cancelled"), ct);
        await tx.CommitAsync(ct);
        return true;
    }
    public async Task<long> PublishAsync(Notification n, CancellationToken ct)
    {
        // Join the moderation/audit transaction when present, including its push outbox writes.
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({n.UserId})", ct);
        var existing = await db.Set<Notification>().Where(x => x.UserId == n.UserId && x.EventKey == n.EventKey)
         .Select(x => (long?)x.Id).SingleOrDefaultAsync(ct);
        if (existing.HasValue) return existing.Value;
        db.Add(n);
        await db.SaveChangesAsync(ct);
        var subscriptions = await db.Set<BrowserPushSubscription>().Where(s => s.UserId == n.UserId && s.IsActive)
         .Select(s => s.Id).ToListAsync(ct);
        db.AddRange(subscriptions.Select(id => new NotificationPushDelivery
        { NotificationId = n.Id, SubscriptionId = id, NextAttemptAt = n.CreatedAt }));
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return n.Id;
    }
    public Task<ModerationNotificationSource?> ModerationDecisionSourceAsync(long id, CancellationToken ct) =>
        (from decision in db.Set<PostModerationDecision>().AsNoTracking()
         join post in db.Posts on decision.PostId equals post.Id
         where decision.Id == id
         select new ModerationNotificationSource(post.AuthorId, post.Id, decision.Action, decision.Reason)).SingleOrDefaultAsync(ct);
    public Task<CommentNotificationSource?> CommentSourceAsync(long id, CancellationToken ct) =>
     (from comment in db.Comments.AsNoTracking()
      join post in db.Posts on comment.PostId equals post.Id
      where comment.Id == id && !comment.IsDeleted && comment.Status == "visible" && !post.IsDeleted && post.Status == "published"
      select new CommentNotificationSource(post.AuthorId, comment.UserId, post.Id)).SingleOrDefaultAsync(ct);
    public Task<Post?> ReviewedPostAsync(long id, CancellationToken ct) =>
     db.Posts.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && !p.IsDeleted && (p.Status == "published" || p.Status == "rejected"), ct);
    public Task<MealReminderSource?> MealSourceAsync(long scheduleId, CancellationToken ct) =>
     (from schedule in db.Set<MealPlanSchedule>().AsNoTracking()
      join plan in db.MealPlans on schedule.MealPlanId equals plan.Id
      where schedule.Id == scheduleId && plan.UserId != null && !plan.IsTemplate
      select new MealReminderSource(plan.UserId!.Value, schedule.DayOfWeek, plan.StartDate, plan.EndDate, plan.Status)).SingleOrDefaultAsync(ct);
    public async Task<List<NotificationPushDelivery>> ClaimPushAsync(DateTime now, Guid leaseToken, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // A worker dying on its final attempt must not leave a permanently processing delivery.
        await db.Set<NotificationPushDelivery>()
         .Where(d => d.State == "processing" && d.LeaseUntil <= now && d.Attempts >= 5)
         .ExecuteUpdateAsync(u => u.SetProperty(d => d.State, "failed").SetProperty(d => d.LastErrorCode, "lease_exhausted"), ct);
        var rows = await db.Set<NotificationPushDelivery>().FromSqlInterpolated($"""
   SELECT * FROM notification_push_deliveries
   WHERE attempts < 5 AND ((state = 'pending' AND next_attempt_at <= {now})
    OR (state = 'processing' AND lease_until <= {now}))
   ORDER BY next_attempt_at, id LIMIT 20 FOR UPDATE SKIP LOCKED
   """).ToListAsync(ct);
        foreach (var row in rows)
        {
            row.State = "processing"; row.Attempts++; row.LeaseToken = leaseToken; row.LeaseUntil = now.AddMinutes(10);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        var ids = rows.Select(d => d.Id).ToArray();
        return await db.Set<NotificationPushDelivery>().AsNoTracking().Include(d => d.Notification).Include(d => d.Subscription)
         .Where(d => ids.Contains(d.Id) && d.LeaseToken == leaseToken).ToListAsync(ct);
    }
    public async Task FinishPushAsync(NotificationPushDelivery delivery, string state, string? error, DateTime nextAttempt, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var changed = await db.Set<NotificationPushDelivery>()
         .Where(d => d.Id == delivery.Id && d.LeaseToken == delivery.LeaseToken && d.State == "processing")
         .ExecuteUpdateAsync(u => u.SetProperty(d => d.State, state).SetProperty(d => d.LastErrorCode, error)
          .SetProperty(d => d.NextAttemptAt, nextAttempt).SetProperty(d => d.LeaseUntil, (DateTime?)null)
          .SetProperty(d => d.LeaseToken, (Guid?)null), ct);
        if (changed > 0 && state == "expired")
            await db.Set<BrowserPushSubscription>()
             .Where(s => s.Id == delivery.SubscriptionId && s.UpdatedAt == delivery.Subscription.UpdatedAt)
             .ExecuteUpdateAsync(u => u.SetProperty(s => s.IsActive, false), ct);
        await tx.CommitAsync(ct);
    }
}
