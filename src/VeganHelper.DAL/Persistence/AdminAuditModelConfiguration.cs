using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Persistence;

internal static class AdminAuditModelConfiguration
{
    internal static void Configure(ModelBuilder builder)
    {
        builder.Entity<AdminAuditLog>(e =>
        {
            e.ToTable("admin_audit_logs", t =>
            {
                t.HasCheckConstraint("CK_admin_audit_logs_required", "length(trim(action)) > 0 AND length(trim(target_type)) > 0 AND length(trim(trace_id)) > 0");
            });
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            e.Property(a => a.AdminId).HasColumnName("admin_id");
            e.Property(a => a.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
            e.Property(a => a.TargetType).HasColumnName("target_type").HasMaxLength(50).IsRequired();
            e.Property(a => a.TargetId).HasColumnName("target_id").HasMaxLength(100);
            e.Property(a => a.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            e.Property(a => a.TraceId).HasColumnName("trace_id").HasMaxLength(100).IsRequired();
            e.Property(a => a.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasOne<User>().WithMany().HasForeignKey(a => a.AdminId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(a => new { a.CreatedAt, a.Id });
            e.HasIndex(a => new { a.AdminId, a.CreatedAt });
            e.HasIndex(a => new { a.TargetType, a.TargetId });
        });
    }
}
