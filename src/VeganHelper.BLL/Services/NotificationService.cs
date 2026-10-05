using AutoMapper;
using System.Security.Cryptography;
using System.Text;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Notifications;
using VeganHelper.BLL.Exceptions;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class NotificationService(INotificationRepository repository, IMapper mapper, TimeProvider clock) : INotificationService, INotificationPublisher
{
    public async Task<PagedResult<NotificationDto>> ListAsync(long userId, NotificationListRequest request, CancellationToken ct)
    {
        Pagination.Validate(request.PageIndex, request.PageSize);
        var (count, items) = await repository.ListAsync(userId, request.PageIndex, request.PageSize, request.UnreadOnly, ct);
        return new PagedResult<NotificationDto>
        {
            Items = mapper.Map<List<NotificationDto>>(items),
            TotalItems = count,
            TotalCount = count,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
            TotalPages = Pagination.TotalPages(count, request.PageSize)
        };
    }
    public async Task<UnreadCountResponse> UnreadCountAsync(long userId, CancellationToken ct) =>
     new(await repository.UnreadCountAsync(userId, ct));
    public async Task<ReadNotificationsResponse> MarkReadAsync(long userId, long id, CancellationToken ct)
    {
        var (exists, changed) = await repository.MarkReadAsync(userId, id, clock.GetUtcNow().UtcDateTime, ct);
        if (!exists) throw new NotFoundException("Notification not found.");
        return new(changed, await repository.UnreadCountAsync(userId, ct));
    }
    public async Task<ReadNotificationsResponse> MarkAllReadAsync(long userId, CancellationToken ct) =>
     new(await repository.MarkAllReadAsync(userId, clock.GetUtcNow().UtcDateTime, ct), await repository.UnreadCountAsync(userId, ct));
    public async Task<PushSubscriptionResponse> SubscribeAsync(long userId, PushSubscriptionRequest request, CancellationToken ct)
    {
        if (!PushSubscriptionValidation.IsAllowedEndpoint(request.Endpoint) || request.Keys is null ||
         !PushSubscriptionValidation.IsKey(request.Keys.P256dh, 65, true) || !PushSubscriptionValidation.IsKey(request.Keys.Auth, 16))
            throw new ArgumentException("Invalid browser push subscription.");
        var id = await repository.SubscribeAsync(new BrowserPushSubscription
        {
            UserId = userId,
            Endpoint = request.Endpoint,
            EndpointHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Endpoint))),
            P256dh = request.Keys.P256dh,
            Auth = request.Keys.Auth,
            IsActive = true,
            UpdatedAt = clock.GetUtcNow().UtcDateTime
        }, ct);
        if (id == 0) throw new ArgumentException("This browser subscription is already registered to another account. Unsubscribe from that account first.");
        if (id < 0) throw new ArgumentException("At most 20 active browser subscriptions are allowed.");
        return new(id);
    }
    public Task<bool> UnsubscribeAsync(long userId, long id, CancellationToken ct) => repository.UnsubscribeAsync(userId, id, ct);
    public async Task<long?> NotifyCommentAsync(long commentId, CancellationToken ct = default)
    {
        var source = await repository.CommentSourceAsync(commentId, ct);
        if (source is null || source.AuthorId == source.CommenterId) return null;
        return await Publish(source.AuthorId, $"comment:{commentId}", "comment", "New comment",
         "Someone commented on your post.", $"/posts/{source.PostId}", ct);
    }
    public async Task<long?> NotifyPostReviewAsync(long postId, CancellationToken ct = default)
    {
        var post = await repository.ReviewedPostAsync(postId, ct);
        if (post is null || !post.UpdatedAt.HasValue) return null;
        return await Publish(post.AuthorId, $"review:{post.Id}:{post.Status}:{post.UpdatedAt.Value.Ticks}", "post_review",
         post.Status == "published" ? "Post approved" : "Post rejected",
         post.Status == "published" ? "Your post has been approved." : "Your post was rejected. Please review it before resubmitting.",
         $"/posts/{post.Id}", ct);
    }
    public async Task<long?> NotifyMealReminderAsync(long scheduleId, DateOnly date, CancellationToken ct = default)
    {
        var source = await repository.MealSourceAsync(scheduleId, ct);
        var isoDay = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
        if (source is null || source.Status != "saved" || source.DayOfWeek != isoDay ||
         source.StartDate is null || source.EndDate is null || date < source.StartDate || date > source.EndDate) return null;
        return await Publish(source.UserId, $"meal:{scheduleId}:{date:yyyy-MM-dd}", "meal_reminder",
         "Meal plan reminder", "Your planned meal is ready to prepare.", "/weekly-menu", ct);
    }
    private Task<long> Publish(long userId, string key, string type, string title, string message, string url, CancellationToken ct) =>
     repository.PublishAsync(new Notification
     {
         UserId = userId,
         EventKey = key,
         Type = type,
         Title = title,
         Message = message,
         TargetUrl = url,
         CreatedAt = clock.GetUtcNow().UtcDateTime
     }, ct);
}

public static class PushSubscriptionValidation
{
    public static bool IsAllowedEndpoint(string? endpoint)
    {
        if (endpoint is null || endpoint.Length > 2048 || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
         uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0) return false;
        var host = uri.IdnHost;
        return host == "fcm.googleapis.com" || host == "updates.push.services.mozilla.com" ||
         host == "web.push.apple.com" || host.EndsWith(".notify.windows.com", StringComparison.OrdinalIgnoreCase);
    }
    public static bool IsKey(string? key, int bytes, bool ecPoint = false)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) return false;
        try
        {
            var raw = Convert.FromBase64String(key.Replace('-', '+').Replace('_', '/').PadRight((key.Length + 3) / 4 * 4, '='));
            if (raw.Length != bytes || (ecPoint && raw[0] != 4)) return false;
            if (ecPoint)
            {
                using var curve = ECDsa.Create(new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    Q = new ECPoint { X = raw[1..33], Y = raw[33..65] }
                });
            }
            return true;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException) { return false; }
    }
}
