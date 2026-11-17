using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddRegistrationDraftCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PendingDisplayName",
                table: "registration_challenges",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingPasswordHash",
                table: "registration_challenges",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingUsername",
                table: "registration_challenges",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingDisplayName",
                table: "registration_challenges");

            migrationBuilder.DropColumn(
                name: "PendingPasswordHash",
                table: "registration_challenges");

            migrationBuilder.DropColumn(
                name: "PendingUsername",
                table: "registration_challenges");
        }
    }
}
