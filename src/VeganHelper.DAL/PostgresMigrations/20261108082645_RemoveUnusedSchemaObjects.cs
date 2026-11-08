using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedSchemaObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Refuse unexpected data loss if another instance has started using these objects.
            migrationBuilder.Sql("""
                SET LOCAL lock_timeout = '5s';
                LOCK TABLE public.post_embeddings, public.user_available_ingredients,
                    public.post_media, public.post_moderation_decisions IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM public.post_embeddings)
                       OR EXISTS (SELECT 1 FROM public.user_available_ingredients)
                       OR EXISTS (SELECT 1 FROM public.post_media WHERE cloud_public_id IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM public.post_moderation_decisions WHERE scan_id IS NOT NULL) THEN
                        RAISE EXCEPTION 'Retired schema objects contain data. Archive and review before removing them.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_post_moderation_decisions_post_moderation_scans_scan_id",
                table: "post_moderation_decisions");

            migrationBuilder.DropTable(
                name: "post_embeddings");

            migrationBuilder.DropTable(
                name: "user_available_ingredients");

            migrationBuilder.DropIndex(
                name: "IX_post_moderation_decisions_scan_id",
                table: "post_moderation_decisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_moderation_decision_actor",
                table: "post_moderation_decisions");

            migrationBuilder.DropColumn(
                name: "scan_id",
                table: "post_moderation_decisions");

            migrationBuilder.DropColumn(
                name: "cloud_public_id",
                table: "post_media");

            migrationBuilder.AddCheckConstraint(
                name: "CK_moderation_decision_actor",
                table: "post_moderation_decisions",
                sql: "(actor_type = 'admin' AND admin_id IS NOT NULL) OR (actor_type = 'ai' AND admin_id IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_moderation_decision_actor",
                table: "post_moderation_decisions");

            migrationBuilder.AddColumn<long>(
                name: "scan_id",
                table: "post_moderation_decisions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cloud_public_id",
                table: "post_media",
                type: "character varying(255)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "post_embeddings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", nullable: false),
                    embedded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    embedding_model = table.Column<string>(type: "character varying(100)", nullable: false),
                    post_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", nullable: false, defaultValueSql: "'pending'"),
                    vector_id = table.Column<string>(type: "character varying(255)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_embeddings", x => x.id);
                    table.UniqueConstraint("UQ_post_embeddings_1", x => new { x.post_id, x.chunk_index });
                    table.CheckConstraint("CK_post_embeddings_1", "chunk_index >= 0");
                    table.CheckConstraint("CK_post_embeddings_2", "status IN ('pending','processing','completed','failed','stale')");
                    table.CheckConstraint("CK_post_embeddings_3", "status <> 'completed' OR (vector_id IS NOT NULL AND embedded_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_post_embeddings_1",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_available_ingredients",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    ingredient_id = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", nullable: true),
                    unit = table.Column<string>(type: "character varying(20)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_available_ingredients", x => new { x.user_id, x.ingredient_id });
                    table.CheckConstraint("CK_user_available_ingredients_1", "quantity >= 0");
                    table.ForeignKey(
                        name: "FK_user_available_ingredients_1",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_user_available_ingredients_2",
                        column: x => x.ingredient_id,
                        principalTable: "ingredients",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_post_moderation_decisions_scan_id",
                table: "post_moderation_decisions",
                column: "scan_id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_moderation_decision_actor",
                table: "post_moderation_decisions",
                sql: "(actor_type = 'admin' AND admin_id IS NOT NULL) OR (actor_type = 'ai' AND admin_id IS NULL AND scan_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_post_embeddings_1",
                table: "post_embeddings",
                columns: new[] { "status", "post_id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_available_ingredients_ingredient_id",
                table: "user_available_ingredients",
                column: "ingredient_id");

            migrationBuilder.AddForeignKey(
                name: "FK_post_moderation_decisions_post_moderation_scans_scan_id",
                table: "post_moderation_decisions",
                column: "scan_id",
                principalTable: "post_moderation_scans",
                principalColumn: "id");

            // Rollback must preserve the original browser-access restrictions.
            migrationBuilder.Sql("""
                ALTER TABLE public.post_embeddings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public.user_available_ingredients ENABLE ROW LEVEL SECURITY;
                REVOKE ALL ON public.post_embeddings, public.user_available_ingredients FROM PUBLIC;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
                        REVOKE ALL ON public.post_embeddings, public.user_available_ingredients FROM anon;
                    END IF;
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
                        REVOKE ALL ON public.post_embeddings, public.user_available_ingredients FROM authenticated;
                    END IF;
                END $$;
                """);
        }
    }
}
