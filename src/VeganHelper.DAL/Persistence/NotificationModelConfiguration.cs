using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
namespace VeganHelper.DAL.Persistence;

internal static class NotificationModelConfiguration
{
    internal static void Configure(ModelBuilder builder)
    {
        builder.Entity<Notification>(e =>
        {
            e.ToTable("notifications", t =>
            {
                t.HasCheckConstraint("CK_notifications_type", "type IN ('comment','post_review','meal_reminder')");
                t.HasCheckConstraint("CK_notifications_read", "is_read = (read_at IS NOT NULL)");
            });
            e.HasKey(n => n.Id);
            e.Property(n => n.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            e.Property(n => n.UserId).HasColumnName("user_id");
            e.Property(n => n.EventKey).HasColumnName("event_key").HasMaxLength(200).IsRequired();
            e.Property(n => n.Type).HasColumnName("type").HasMaxLength(30).IsRequired();
            e.Property(n => n.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            e.Property(n => n.Message).HasColumnName("message").HasMaxLength(1000).IsRequired();
            e.Property(n => n.TargetUrl).HasColumnName("target_url").HasMaxLength(300).IsRequired();
            e.Property(n => n.IsRead).HasColumnName("is_read");
            e.Property(n => n.CreatedAt).HasColumnName("created_at");
            e.Property(n => n.ReadAt).HasColumnName("read_at");
            e.HasOne<User>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(n => new { n.UserId, n.EventKey }).IsUnique();
            e.HasIndex(n => new { n.UserId, n.CreatedAt, n.Id });
            e.HasIndex(n => n.UserId).HasFilter("is_read = false");
        });
        builder.Entity<BrowserPushSubscription>(e =>
        {
            e.ToTable("browser_push_subscriptions");
            e.HasKey(n => n.Id);
            e.Property(n => n.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            e.Property(n => n.UserId).HasColumnName("user_id");
            e.Property(n => n.Endpoint).HasColumnName("endpoint").HasMaxLength(2048).IsRequired();
            e.Property(n => n.EndpointHash).HasColumnName("endpoint_hash").HasMaxLength(64).IsRequired();
            e.Property(n => n.P256dh).HasColumnName("p256dh").HasMaxLength(100).IsRequired();
            e.Property(n => n.Auth).HasColumnName("auth").HasMaxLength(30).IsRequired();
            e.Property(n => n.IsActive).HasColumnName("is_active");
            e.Property(n => n.UpdatedAt).HasColumnName("updated_at");
            e.HasOne<User>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(n => n.EndpointHash).IsUnique();
            e.HasIndex(n => new { n.UserId, n.IsActive });
        });
        builder.Entity<NotificationPushDelivery>(e =>
        {
            e.ToTable("notification_push_deliveries", t =>
            {
                t.HasCheckConstraint("CK_notification_delivery_state", "state IN ('pending','processing','sent','failed','expired','cancelled')");
                t.HasCheckConstraint("CK_notification_delivery_attempts", "attempts BETWEEN 0 AND 5");
            });
            e.HasKey(n => n.Id);
            e.Property(n => n.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            e.Property(n => n.NotificationId).HasColumnName("notification_id");
            e.Property(n => n.SubscriptionId).HasColumnName("subscription_id");
            e.Property(n => n.State).HasColumnName("state").HasMaxLength(20).IsRequired();
            e.Property(n => n.Attempts).HasColumnName("attempts");
            e.Property(n => n.NextAttemptAt).HasColumnName("next_attempt_at");
            e.Property(n => n.LeaseUntil).HasColumnName("lease_until");
            e.Property(n => n.LeaseToken).HasColumnName("lease_token");
            e.Property(n => n.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(50);
            e.HasOne(n => n.Notification).WithMany().HasForeignKey(n => n.NotificationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(n => n.Subscription).WithMany().HasForeignKey(n => n.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(n => new { n.NotificationId, n.SubscriptionId }).IsUnique();
            e.HasIndex(n => new { n.State, n.NextAttemptAt });
        });
    }
}
