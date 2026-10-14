using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddAdminMemberBans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "token_version",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "token_version",
                table: "refresh_tokens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "user_bans",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    banned_by_admin_id = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    unbanned_by_admin_id = table.Column<long>(type: "bigint", nullable: true),
                    unban_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    unbanned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_bans", x => x.id);
                    table.CheckConstraint("CK_user_bans_expiry", "expires_at IS NULL OR expires_at > created_at");
                    table.CheckConstraint("CK_user_bans_reason", "length(trim(reason)) > 0");
                    table.CheckConstraint("CK_user_bans_unban", "(unbanned_at IS NULL AND unbanned_by_admin_id IS NULL AND unban_reason IS NULL) OR (unbanned_at IS NOT NULL AND unbanned_at >= created_at AND unbanned_by_admin_id IS NOT NULL AND unban_reason IS NOT NULL AND length(trim(unban_reason)) > 0)");
                    table.ForeignKey(
                        name: "FK_user_bans_users_banned_by_admin_id",
                        column: x => x.banned_by_admin_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_user_bans_users_unbanned_by_admin_id",
                        column: x => x.unbanned_by_admin_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_user_bans_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_bans_banned_by_admin_id",
                table: "user_bans",
                column: "banned_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_bans_unbanned_by_admin_id",
                table: "user_bans",
                column: "unbanned_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_bans_user_id_unbanned_at_expires_at",
                table: "user_bans",
                columns: new[] { "user_id", "unbanned_at", "expires_at" });

            // This table is read/written exclusively through the backend connection, never the Supabase Data API.
            migrationBuilder.Sql("""
                ALTER TABLE public.user_bans ENABLE ROW LEVEL SECURITY;
                REVOKE ALL ON TABLE public.user_bans FROM PUBLIC;
                DO $$ DECLARE role_name text; sequence_name text;
                BEGIN
                    sequence_name := pg_get_serial_sequence('public.user_bans', 'id');
                    EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM PUBLIC', sequence_name);
                    FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated'] LOOP
                        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
                            EXECUTE format('REVOKE ALL ON TABLE public.user_bans FROM %I', role_name);
                            EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM %I', sequence_name, role_name);
                        END IF;
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_bans");

            migrationBuilder.DropColumn(
                name: "token_version",
                table: "users");

            migrationBuilder.DropColumn(
                name: "token_version",
                table: "refresh_tokens");
        }
    }
}
