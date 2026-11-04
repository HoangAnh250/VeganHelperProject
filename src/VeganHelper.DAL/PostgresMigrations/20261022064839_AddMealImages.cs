using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddMealImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "image_url",
                table: "meals",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "image_url",
                table: "meals");
        }
    }
}
