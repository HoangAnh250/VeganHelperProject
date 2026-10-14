using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddUserFollows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_follows",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    follower_id = table.Column<long>(type: "bigint", nullable: false),
                    following_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_follows", x => x.id);
                    table.CheckConstraint("CK_user_follows_no_self", "follower_id <> following_id");
                    table.ForeignKey(
                        name: "FK_user_follows_follower",
                        column: x => x.follower_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_user_follows_following",
                        column: x => x.following_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_follows_follower_created",
                table: "user_follows",
                columns: new[] { "follower_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_follows_following_created",
                table: "user_follows",
                columns: new[] { "following_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "UQ_user_follows_pair",
                table: "user_follows",
                columns: new[] { "follower_id", "following_id" },
                unique: true);

            migrationBuilder.Sql("""
                DO $$
                DECLARE sequence_name text; role_name text;
                BEGIN
                    EXECUTE 'ALTER TABLE public.user_follows ENABLE ROW LEVEL SECURITY';
                    EXECUTE 'REVOKE ALL ON TABLE public.user_follows FROM PUBLIC';
                    sequence_name := pg_get_serial_sequence('public.user_follows', 'id');
                    EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM PUBLIC', sequence_name);
                    FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated']
                    LOOP
                        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
                            EXECUTE format('REVOKE ALL ON TABLE public.user_follows FROM %I', role_name);
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
                name: "user_follows");
        }
    }
}
