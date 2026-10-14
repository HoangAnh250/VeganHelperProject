using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeganHelper.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthProfileAndBmiHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "activity_level",
                table: "user_profiles",
                type: "NVARCHAR(30)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "current_bmi",
                table: "user_profiles",
                type: "DECIMAL(5,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bmi_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "BIGINT", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<long>(type: "BIGINT", nullable: false),
                    height_cm = table.Column<decimal>(type: "DECIMAL(5,2)", nullable: false),
                    weight_kg = table.Column<decimal>(type: "DECIMAL(6,2)", nullable: false),
                    bmi_value = table.Column<decimal>(type: "DECIMAL(5,2)", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bmi_history", x => x.id);
                    table.CheckConstraint("CK_bmi_history_1", "height_cm > 0");
                    table.CheckConstraint("CK_bmi_history_2", "weight_kg > 0");
                    table.CheckConstraint("CK_bmi_history_3", "bmi_value > 0");
                    table.ForeignKey(
                        name: "FK_bmi_history_1",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_user_profiles_5",
                table: "user_profiles",
                sql: "activity_level IN ('sedentary','light','moderate','active','very_active')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_user_profiles_6",
                table: "user_profiles",
                sql: "current_bmi IS NULL OR current_bmi > 0");

            migrationBuilder.CreateIndex(
                name: "IX_bmi_history_1",
                table: "bmi_history",
                columns: new[] { "user_id", "recorded_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bmi_history");

            migrationBuilder.DropCheckConstraint(
                name: "CK_user_profiles_5",
                table: "user_profiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_user_profiles_6",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "activity_level",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "current_bmi",
                table: "user_profiles");
        }
    }
}
