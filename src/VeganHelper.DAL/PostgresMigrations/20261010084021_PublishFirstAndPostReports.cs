using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class PublishFirstAndPostReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_flags_reporter_id",
                table: "flags");

            migrationBuilder.AddColumn<bool>(
                name: "moderation_hold",
                table: "posts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "provider_retry_after",
                table: "post_moderation_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "priority",
                table: "post_moderation_scans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "report_recheck_requested",
                table: "post_moderation_scans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "reason_category",
                table: "flags",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reported_snapshot_json",
                table: "flags",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "post_moderation_assessments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    post_id = table.Column<long>(type: "bigint", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_moderation_assessments", x => x.id);
                    table.ForeignKey(
                        name: "FK_post_moderation_assessments_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id");
                });

            migrationBuilder.UpdateData(
                table: "post_moderation_settings",
                keyColumn: "id",
                keyValue: 1,
                column: "provider_retry_after",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_flags_reporter_id_created_at",
                table: "flags",
                columns: new[] { "reporter_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_flags_reporter_id_post_id_post_revision",
                table: "flags",
                columns: new[] { "reporter_id", "post_id", "post_revision" },
                unique: true,
                filter: "source_type = 'user' AND status = 'pending' AND post_revision IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_post_moderation_assessments_post_id",
                table: "post_moderation_assessments",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "IX_post_moderation_assessments_request_id",
                table: "post_moderation_assessments",
                column: "request_id",
                unique: true);
            migrationBuilder.Sql("""
                UPDATE posts SET moderation_hold = true WHERE status IN ('pending_review','hidden','rejected');
                UPDATE post_moderation_settings SET auto_publish_enabled = false;
                INSERT INTO post_moderation_assessments (post_id, revision, request_id, result_json, created_at)
                SELECT post_id, revision, md5('legacy-scan-' || id::text)::uuid,
                  jsonb_build_object('state',state,'errorCode',error_code,'model',model,'summary',summary,
                    'confidence',confidence,'findings',findings_json,'legacy',true), COALESCE(completed_at,created_at)
                FROM post_moderation_scans WHERE state IN ('safe','flagged','manual_required','failed');
                ALTER TABLE post_moderation_assessments ENABLE ROW LEVEL SECURITY;
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
                    REVOKE ALL ON post_moderation_assessments FROM anon;
                    REVOKE ALL ON SEQUENCE post_moderation_assessments_id_seq FROM anon;
                  END IF;
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
                    REVOKE ALL ON post_moderation_assessments FROM authenticated;
                    REVOKE ALL ON SEQUENCE post_moderation_assessments_id_seq FROM authenticated;
                  END IF;
                END $$;
                CREATE FUNCTION protect_post_assessment() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Moderation assessment history is append-only'; END $$;
                CREATE TRIGGER protect_post_assessment BEFORE UPDATE OR DELETE ON post_moderation_assessments
                  FOR EACH ROW EXECUTE FUNCTION protect_post_assessment();
                CREATE TRIGGER protect_post_assessment_truncate BEFORE TRUNCATE ON post_moderation_assessments
                  FOR EACH STATEMENT EXECUTE FUNCTION protect_post_assessment();
                REVOKE ALL ON FUNCTION protect_post_assessment() FROM PUBLIC;
                CREATE FUNCTION protect_post_report_evidence() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF OLD.reported_snapshot_json IS NOT NULL AND
                    (NEW.reported_snapshot_json IS DISTINCT FROM OLD.reported_snapshot_json OR
                     NEW.reason IS DISTINCT FROM OLD.reason OR NEW.reason_category IS DISTINCT FROM OLD.reason_category OR
                     NEW.post_revision IS DISTINCT FROM OLD.post_revision OR NEW.reporter_id IS DISTINCT FROM OLD.reporter_id OR
                     NEW.post_id IS DISTINCT FROM OLD.post_id OR NEW.source_type IS DISTINCT FROM OLD.source_type) THEN
                    RAISE EXCEPTION 'Report evidence is immutable';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER protect_post_report_evidence BEFORE UPDATE ON flags
                  FOR EACH ROW EXECUTE FUNCTION protect_post_report_evidence();
                REVOKE ALL ON FUNCTION protect_post_report_evidence() FROM PUBLIC;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER protect_post_report_evidence ON flags;
                DROP FUNCTION protect_post_report_evidence();
                DROP TRIGGER protect_post_assessment ON post_moderation_assessments;
                DROP TRIGGER protect_post_assessment_truncate ON post_moderation_assessments;
                DROP FUNCTION protect_post_assessment();
                """);
            migrationBuilder.DropTable(
                name: "post_moderation_assessments");

            migrationBuilder.DropIndex(
                name: "IX_flags_reporter_id_created_at",
                table: "flags");

            migrationBuilder.DropIndex(
                name: "IX_flags_reporter_id_post_id_post_revision",
                table: "flags");

            migrationBuilder.DropColumn(
                name: "moderation_hold",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "provider_retry_after",
                table: "post_moderation_settings");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "post_moderation_scans");

            migrationBuilder.DropColumn(
                name: "report_recheck_requested",
                table: "post_moderation_scans");

            migrationBuilder.DropColumn(
                name: "reason_category",
                table: "flags");

            migrationBuilder.DropColumn(
                name: "reported_snapshot_json",
                table: "flags");

            migrationBuilder.CreateIndex(
                name: "IX_flags_reporter_id",
                table: "flags",
                column: "reporter_id");
        }
    }
}
