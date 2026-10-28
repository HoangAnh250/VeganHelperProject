using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddMultiVideoPostSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_post_summaries_2",
                table: "post_summaries");

            migrationBuilder.DropIndex(
                name: "IX_post_summaries_post_id_source_media_id",
                table: "post_summaries");

            migrationBuilder.AddColumn<int>(
                name: "content_revision",
                table: "post_summaries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "source_media_ids_json",
                table: "post_summaries",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.Sql("""
                UPDATE post_summaries AS s
                SET source_media_ids_json = '[' || s.source_media_id::text || ']',
                    content_revision = p.content_revision
                FROM posts AS p
                WHERE p.id = s.post_id;
                """);

            migrationBuilder.DropColumn(
                name: "source_media_id",
                table: "post_summaries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "content_revision",
                table: "post_summaries");

            migrationBuilder.AddColumn<long>(
                name: "source_media_id",
                table: "post_summaries",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql("""
                UPDATE post_summaries
                SET source_media_id = (source_media_ids_json::jsonb ->> 0)::bigint
                WHERE jsonb_array_length(source_media_ids_json::jsonb) > 0;
                """);

            migrationBuilder.DropColumn(
                name: "source_media_ids_json",
                table: "post_summaries");

            migrationBuilder.CreateIndex(
                name: "IX_post_summaries_post_id_source_media_id",
                table: "post_summaries",
                columns: new[] { "post_id", "source_media_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_post_summaries_2",
                table: "post_summaries",
                columns: new[] { "post_id", "source_media_id" },
                principalTable: "post_media",
                principalColumns: new[] { "post_id", "id" });
        }
    }
}
