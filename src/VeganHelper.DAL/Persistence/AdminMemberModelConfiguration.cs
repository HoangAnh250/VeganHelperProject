using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Persistence;

internal static class AdminMemberModelConfiguration
{
    internal static void Configure(ModelBuilder builder)
    {
        builder.Entity<UserBan>(e =>
        {
            e.ToTable("user_bans", t =>
            {
                t.HasCheckConstraint("CK_user_bans_reason", "length(trim(reason)) > 0");
                t.HasCheckConstraint("CK_user_bans_expiry", "expires_at IS NULL OR expires_at > created_at");
                t.HasCheckConstraint("CK_user_bans_unban", "(unbanned_at IS NULL AND unbanned_by_admin_id IS NULL AND unban_reason IS NULL) OR (unbanned_at IS NOT NULL AND unbanned_at >= created_at AND unbanned_by_admin_id IS NOT NULL AND unban_reason IS NOT NULL AND length(trim(unban_reason)) > 0)");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.BannedByAdminId).HasColumnName("banned_by_admin_id");
            e.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone");
            e.Property(x => x.UnbannedByAdminId).HasColumnName("unbanned_by_admin_id");
            e.Property(x => x.UnbanReason).HasColumnName("unban_reason").HasMaxLength(500);
            e.Property(x => x.UnbannedAt).HasColumnName("unbanned_at").HasColumnType("timestamp with time zone");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.BannedByAdminId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UnbannedByAdminId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => new { x.UserId, x.UnbannedAt, x.ExpiresAt });
        });
    }
}
