using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Persistence;

internal static class PostModerationModelConfiguration
{
    internal static void Configure(ModelBuilder b)
    {
        b.Entity<Post>().Property(p => p.ContentRevision).HasColumnName("content_revision").HasDefaultValue(1).IsConcurrencyToken();
        b.Entity<Flag>().Property(f => f.PostRevision).HasColumnName("post_revision");
        b.Entity<Flag>().Property(f => f.ModerationScanId).HasColumnName("moderation_scan_id");
        b.Entity<Flag>().HasOne<PostModerationScan>().WithMany().HasForeignKey(f => f.ModerationScanId).OnDelete(DeleteBehavior.NoAction);
        b.Entity<PostModerationScan>(e =>
        {
            e.ToTable("post_moderation_scans", t =>
            {
                t.HasCheckConstraint("CK_moderation_scan_state", "state IN ('queued','processing','safe','flagged','manual_required','failed','obsolete')");
                t.HasCheckConstraint("CK_moderation_scan_attempts", "attempts BETWEEN 0 AND 5 AND revision > 0");
                t.HasCheckConstraint("CK_moderation_scan_confidence", "confidence IS NULL OR (confidence >= 0 AND confidence <= 1)");
            });
            e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            e.Property(x => x.PostId).HasColumnName("post_id"); e.Property(x => x.Revision).HasColumnName("revision");
            e.Property(x => x.State).HasColumnName("state").HasMaxLength(20); e.Property(x => x.Attempts).HasColumnName("attempts");
            e.Property(x => x.LeaseToken).HasColumnName("lease_token"); e.Property(x => x.LeaseUntil).HasColumnName("lease_until");
            e.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at"); e.Property(x => x.Model).HasColumnName("model").HasMaxLength(100);
            e.Property(x => x.PromptVersion).HasColumnName("prompt_version").HasMaxLength(30); e.Property(x => x.Confidence).HasColumnName("confidence");
            e.Property(x => x.Summary).HasColumnName("summary").HasMaxLength(500); e.Property(x => x.FindingsJson).HasColumnName("findings_json").HasColumnType("jsonb");
            e.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(50); e.Property(x => x.InputTokens).HasColumnName("input_tokens");
            e.Property(x => x.OutputTokens).HasColumnName("output_tokens"); e.Property(x => x.LatencyMs).HasColumnName("latency_ms");
            e.Property(x => x.CreatedAt).HasColumnName("created_at"); e.Property(x => x.CompletedAt).HasColumnName("completed_at");
            e.HasOne(x => x.Post).WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => new { x.PostId, x.Revision }).IsUnique(); e.HasIndex(x => new { x.State, x.NextAttemptAt });
        });
        b.Entity<PostModerationDecision>(e =>
        {
            e.ToTable("post_moderation_decisions", t =>
            {
                t.HasCheckConstraint("CK_moderation_decision_actor", "(actor_type = 'admin' AND admin_id IS NOT NULL) OR (actor_type = 'ai' AND admin_id IS NULL AND scan_id IS NOT NULL)");
                t.HasCheckConstraint("CK_moderation_decision_action", "action IN ('approve','reject','keep','remove') AND revision > 0 AND length(trim(reason)) > 0");
            });
            e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            e.Property(x => x.PostId).HasColumnName("post_id"); e.Property(x => x.Revision).HasColumnName("revision");
            e.Property(x => x.Action).HasColumnName("action").HasMaxLength(20); e.Property(x => x.ActorType).HasColumnName("actor_type").HasMaxLength(10);
            e.Property(x => x.AdminId).HasColumnName("admin_id"); e.Property(x => x.FlagId).HasColumnName("flag_id"); e.Property(x => x.ScanId).HasColumnName("scan_id");
            e.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1000); e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.AdminId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<Flag>().WithMany().HasForeignKey(x => x.FlagId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<PostModerationScan>().WithMany().HasForeignKey(x => x.ScanId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => new { x.PostId, x.Revision, x.CreatedAt });
        });
        b.Entity<PostModerationSettings>(e =>
        {
            e.ToTable("post_moderation_settings", t => t.HasCheckConstraint("CK_moderation_settings_singleton", "id = 1 AND version > 0"));
            e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.AutoPublishEnabled).HasColumnName("auto_publish_enabled"); e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at"); e.Property(x => x.UpdatedByAdminId).HasColumnName("updated_by_admin_id");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedByAdminId).OnDelete(DeleteBehavior.NoAction);
            e.HasData(new PostModerationSettings());
        });
    }
}
