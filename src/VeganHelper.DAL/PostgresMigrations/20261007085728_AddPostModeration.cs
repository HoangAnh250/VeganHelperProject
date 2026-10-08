using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddPostModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "content_revision",
                table: "posts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<long>(
                name: "moderation_scan_id",
                table: "flags",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "post_revision",
                table: "flags",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "post_moderation_scans",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    post_id = table.Column<long>(type: "bigint", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    prompt_version = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    findings_json = table.Column<string>(type: "jsonb", nullable: false),
                    error_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    input_tokens = table.Column<int>(type: "integer", nullable: true),
                    output_tokens = table.Column<int>(type: "integer", nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_moderation_scans", x => x.id);
                    table.CheckConstraint("CK_moderation_scan_attempts", "attempts BETWEEN 0 AND 5 AND revision > 0");
                    table.CheckConstraint("CK_moderation_scan_confidence", "confidence IS NULL OR (confidence >= 0 AND confidence <= 1)");
                    table.CheckConstraint("CK_moderation_scan_state", "state IN ('queued','processing','safe','flagged','manual_required','failed','obsolete')");
                    table.ForeignKey(
                        name: "FK_post_moderation_scans_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "post_moderation_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    auto_publish_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    updated_by_admin_id = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_moderation_settings", x => x.id);
                    table.CheckConstraint("CK_moderation_settings_singleton", "id = 1 AND version > 0");
                    table.ForeignKey(
                        name: "FK_post_moderation_settings_users_updated_by_admin_id",
                        column: x => x.updated_by_admin_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "post_moderation_decisions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    post_id = table.Column<long>(type: "bigint", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    admin_id = table.Column<long>(type: "bigint", nullable: true),
                    flag_id = table.Column<long>(type: "bigint", nullable: true),
                    scan_id = table.Column<long>(type: "bigint", nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_moderation_decisions", x => x.id);
                    table.CheckConstraint("CK_moderation_decision_action", "action IN ('approve','reject','keep','remove') AND revision > 0 AND length(trim(reason)) > 0");
                    table.CheckConstraint("CK_moderation_decision_actor", "(actor_type = 'admin' AND admin_id IS NOT NULL) OR (actor_type = 'ai' AND admin_id IS NULL AND scan_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_post_moderation_decisions_flags_flag_id",
                        column: x => x.flag_id,
                        principalTable: "flags",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_post_moderation_decisions_post_moderation_scans_scan_id",
                        column: x => x.scan_id,
                        principalTable: "post_moderation_scans",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_post_moderation_decisions_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_post_moderation_decisions_users_admin_id",
                        column: x => x.admin_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.InsertData(
                table: "post_moderation_settings",
                columns: new[] { "id", "auto_publish_enabled", "updated_at", "updated_by_admin_id", "version" },
                values: new object[] { 1, false, null, null, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_flags_moderation_scan_id",
                table: "flags",
                column: "moderation_scan_id");

            migrationBuilder.CreateIndex(
                name: "IX_post_moderation_decisions_admin_id",
                table: "post_moderation_decisions",
                column: "admin_id");

            migrationBuilder.CreateIndex(
                name: "IX_post_moderation_decisions_flag_id",
                table: "post_moderation_decisions",
                column: "flag_id");

            migrationBuilder.CreateIndex(
                name: "IX_post_moderation_decisions_post_id_revision_created_at",
                table: "post_moderation_decisions",
                columns: new[] { "post_id", "revision", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_post_moderation_decisions_scan_id",
                table: "post_moderation_decisions",
                column: "scan_id");

            migrationBuilder.CreateIndex(
                name: "IX_post_moderation_scans_post_id_revision",
                table: "post_moderation_scans",
                columns: new[] { "post_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_post_moderation_scans_state_next_attempt_at",
                table: "post_moderation_scans",
                columns: new[] { "state", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "IX_post_moderation_settings_updated_by_admin_id",
                table: "post_moderation_settings",
                column: "updated_by_admin_id");

            migrationBuilder.AddForeignKey(
                name: "FK_flags_post_moderation_scans_moderation_scan_id",
                table: "flags",
                column: "moderation_scan_id",
                principalTable: "post_moderation_scans",
                principalColumn: "id");
            migrationBuilder.Sql("""
                -- Freeze legacy evidence to its original content version, never a later edit.
                UPDATE public.flags f SET post_revision = p.content_revision
                FROM public.posts p WHERE f.post_id = p.id AND f.source_type = 'ai' AND f.post_revision IS NULL;

                INSERT INTO public.post_moderation_scans
                    (post_id, revision, state, attempts, next_attempt_at, findings_json, created_at)
                SELECT id, content_revision, 'queued', 0, CURRENT_TIMESTAMP, '[]'::jsonb, CURRENT_TIMESTAMP
                FROM public.posts WHERE status = 'pending_review' AND NOT is_deleted;

                DO $$ DECLARE table_name text; role_name text; sequence_name text;
                BEGIN
                    FOREACH table_name IN ARRAY ARRAY['post_moderation_scans','post_moderation_decisions','post_moderation_settings'] LOOP
                        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', table_name);
                        EXECUTE format('REVOKE ALL ON TABLE public.%I FROM PUBLIC', table_name);
                        sequence_name := pg_get_serial_sequence('public.' || table_name, 'id');
                        IF sequence_name IS NOT NULL THEN EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM PUBLIC', sequence_name); END IF;
                        FOREACH role_name IN ARRAY ARRAY['anon','authenticated'] LOOP
                            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
                                EXECUTE format('REVOKE ALL ON TABLE public.%I FROM %I', table_name, role_name);
                                IF sequence_name IS NOT NULL THEN EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM %I', sequence_name, role_name); END IF;
                            END IF;
                        END LOOP;
                    END LOOP;
                END $$;
                CREATE FUNCTION public.reject_post_moderation_mutation() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    RAISE EXCEPTION 'Moderation decisions are append-only.' USING ERRCODE = '42501';
                    RETURN NULL;
                END $$;
                REVOKE ALL ON FUNCTION public.reject_post_moderation_mutation() FROM PUBLIC;
                CREATE TRIGGER moderation_decision_no_update_delete BEFORE UPDATE OR DELETE ON public.post_moderation_decisions
                    FOR EACH ROW EXECUTE FUNCTION public.reject_post_moderation_mutation();
                CREATE TRIGGER moderation_decision_no_truncate BEFORE TRUNCATE ON public.post_moderation_decisions
                    FOR EACH STATEMENT EXECUTE FUNCTION public.reject_post_moderation_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_flags_post_moderation_scans_moderation_scan_id",
                table: "flags");

            migrationBuilder.DropTable(
                name: "post_moderation_decisions");
            migrationBuilder.Sql("DROP FUNCTION public.reject_post_moderation_mutation();");

            migrationBuilder.DropTable(
                name: "post_moderation_settings");

            migrationBuilder.DropTable(
                name: "post_moderation_scans");

            migrationBuilder.DropIndex(
                name: "IX_flags_moderation_scan_id",
                table: "flags");

            migrationBuilder.DropColumn(
                name: "content_revision",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "moderation_scan_id",
                table: "flags");

            migrationBuilder.DropColumn(
                name: "post_revision",
                table: "flags");
        }
    }
}
