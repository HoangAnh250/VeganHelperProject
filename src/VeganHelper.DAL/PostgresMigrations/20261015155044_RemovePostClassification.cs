using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class RemovePostClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_posts_1",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "post_type",
                table: "posts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "post_type",
                table: "posts",
                type: "character varying(20)",
                nullable: false,
                // Original classification labels cannot be reconstructed after Up.
                defaultValue: "community");

            migrationBuilder.AddCheckConstraint(
                name: "CK_posts_1",
                table: "posts",
                sql: "post_type IN ('article','video','recipe','community')");
        }
    }
}
