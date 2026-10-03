using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddAuthenticationSecurityFlows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_email_verification_tokens_user_expiry",
                table: "email_verification_tokens");

            migrationBuilder.AddColumn<string>(
                name: "pending_email",
                table: "users",
                type: "character varying(255)",
                nullable: true,
                collation: "veganhelper_ci");

            migrationBuilder.AddColumn<string>(
                name: "purpose",
                table: "email_verification_tokens",
                type: "character varying(40)",
                nullable: false,
                defaultValueSql: "'registration'");

            migrationBuilder.AddColumn<string>(
                name: "target_email",
                table: "email_verification_tokens",
                type: "character varying(255)",
                nullable: true,
                collation: "veganhelper_ci");

            migrationBuilder.CreateIndex(
                name: "IX_email_verification_tokens_user_purpose_expiry",
                table: "email_verification_tokens",
                columns: new[] { "user_id", "purpose", "expires_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_email_verification_tokens_user_purpose_expiry",
                table: "email_verification_tokens");

            migrationBuilder.DropColumn(
                name: "pending_email",
                table: "users");

            migrationBuilder.DropColumn(
                name: "purpose",
                table: "email_verification_tokens");

            migrationBuilder.DropColumn(
                name: "target_email",
                table: "email_verification_tokens");

            migrationBuilder.CreateIndex(
                name: "IX_email_verification_tokens_user_expiry",
                table: "email_verification_tokens",
                columns: new[] { "user_id", "expires_at" });
        }
    }
}
