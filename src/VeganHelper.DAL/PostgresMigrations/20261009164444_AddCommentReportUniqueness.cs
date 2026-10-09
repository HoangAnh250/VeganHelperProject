using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddCommentReportUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_flags_comment_id",
                table: "flags");

            migrationBuilder.CreateIndex(
                name: "UX_flags_user_comment_report",
                table: "flags",
                columns: new[] { "comment_id", "reporter_id" },
                unique: true,
                filter: "source_type = 'user' AND comment_id IS NOT NULL AND reporter_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_flags_user_comment_report",
                table: "flags");

            migrationBuilder.CreateIndex(
                name: "IX_flags_comment_id",
                table: "flags",
                column: "comment_id");
        }
    }
}
