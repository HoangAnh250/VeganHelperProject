using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class ScopeEmailVerificationCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_email_verification_tokens_hash",
                table: "email_verification_tokens");

            migrationBuilder.AddColumn<int>(
                name: "failed_attempts",
                table: "email_verification_tokens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_email_verification_tokens_user_created",
                table: "email_verification_tokens",
                columns: new[] { "user_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_email_verification_tokens_user_created",
                table: "email_verification_tokens");

            migrationBuilder.DropColumn(
                name: "failed_attempts",
                table: "email_verification_tokens");

            migrationBuilder.CreateIndex(
                name: "UQ_email_verification_tokens_hash",
                table: "email_verification_tokens",
                column: "token_hash",
                unique: true);
        }
    }
}
