using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class VerifyRegistrationBeforeAccountCreation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "registration_challenges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OtpHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    ProofHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ProofExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registration_challenges", x => x.Id);
                });

            migrationBuilder.Sql("""
                ALTER TABLE public.registration_challenges ENABLE ROW LEVEL SECURITY;
                REVOKE ALL ON TABLE public.registration_challenges FROM PUBLIC;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
                        REVOKE ALL ON TABLE public.registration_challenges FROM anon;
                    END IF;
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
                        REVOKE ALL ON TABLE public.registration_challenges FROM authenticated;
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_registration_challenges_Email_CreatedAt",
                table: "registration_challenges",
                columns: new[] { "Email", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_registration_challenges_ProofHash",
                table: "registration_challenges",
                column: "ProofHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registration_challenges");
        }
    }
}
