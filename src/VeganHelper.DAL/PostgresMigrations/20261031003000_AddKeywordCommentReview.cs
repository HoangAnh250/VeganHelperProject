using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations;

public partial class AddKeywordCommentReview : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_flags_4", "flags");
        migrationBuilder.AddCheckConstraint("CK_flags_4", "flags",
            "(source_type = 'user' AND reporter_id IS NOT NULL AND ai_model_name IS NULL) OR (source_type = 'ai' AND reporter_id IS NULL AND ai_model_name IS NOT NULL AND post_id IS NOT NULL) OR (source_type = 'keyword' AND reporter_id IS NULL AND ai_model_name IS NULL AND comment_id IS NOT NULL)");
        migrationBuilder.CreateIndex(
            name: "UX_flags_pending_keyword_comment", table: "flags", column: "comment_id",
            unique: true, filter: "source_type = 'keyword' AND status = 'pending'");
        migrationBuilder.Sql("""
            INSERT INTO flags (comment_id, source_type, reason_category, reason, status, created_at)
            SELECT c.id, 'keyword', 'keyword_filter',
                   'Previously hidden comment without a moderation record; manual review required.',
                   'pending', CURRENT_TIMESTAMP
            FROM comments c
            WHERE c.status = 'hidden' AND NOT c.is_deleted
              AND NOT EXISTS (SELECT 1 FROM flags f WHERE f.comment_id = c.id)
            ON CONFLICT (comment_id) WHERE source_type = 'keyword' AND status = 'pending' DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM flags WHERE source_type = 'keyword') THEN
                    RAISE EXCEPTION 'Cannot roll back while keyword review records exist. Preserve and migrate them before rollback.';
                END IF;
            END $$;
            """);
        migrationBuilder.DropIndex("UX_flags_pending_keyword_comment", "flags");
        migrationBuilder.DropCheckConstraint("CK_flags_4", "flags");
        migrationBuilder.AddCheckConstraint("CK_flags_4", "flags",
            "(source_type = 'user' AND reporter_id IS NOT NULL AND ai_model_name IS NULL) OR (source_type = 'ai' AND reporter_id IS NULL AND ai_model_name IS NOT NULL AND post_id IS NOT NULL)");
    }
}
