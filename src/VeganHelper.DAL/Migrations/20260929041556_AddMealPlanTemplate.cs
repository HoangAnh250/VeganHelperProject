using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeganHelper.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddMealPlanTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_meal_plans_1",
                table: "meal_plans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_meal_plans_2",
                table: "meal_plans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_meal_plans_6",
                table: "meal_plans");

            migrationBuilder.AlterColumn<decimal>(
                name: "weight_kg",
                table: "meal_plans",
                type: "DECIMAL(6,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "DECIMAL(6,2)");

            migrationBuilder.AlterColumn<long>(
                name: "user_id",
                table: "meal_plans",
                type: "BIGINT",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "BIGINT");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "start_date",
                table: "meal_plans",
                type: "DATE",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "DATE");

            migrationBuilder.AlterColumn<decimal>(
                name: "height_cm",
                table: "meal_plans",
                type: "DECIMAL(5,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "DECIMAL(5,2)");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "end_date",
                table: "meal_plans",
                type: "DATE",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "DATE");

            migrationBuilder.AlterColumn<decimal>(
                name: "bmi_value",
                table: "meal_plans",
                type: "DECIMAL(6,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "DECIMAL(6,2)");

            migrationBuilder.AddColumn<bool>(
                name: "is_template",
                table: "meal_plans",
                type: "BIT",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "template_name",
                table: "meal_plans",
                type: "NVARCHAR(255)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_meal_plans_1",
                table: "meal_plans",
                sql: "start_date IS NULL OR DATEDIFF(DAY, start_date, end_date) = 6");

            migrationBuilder.AddCheckConstraint(
                name: "CK_meal_plans_2",
                table: "meal_plans",
                sql: "height_cm IS NULL OR (height_cm > 0 AND weight_kg > 0 AND bmi_value > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_meal_plans_6",
                table: "meal_plans",
                sql: "generation_source IN ('ai','manual','template')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_meal_plans_1",
                table: "meal_plans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_meal_plans_2",
                table: "meal_plans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_meal_plans_6",
                table: "meal_plans");

            migrationBuilder.DropColumn(
                name: "is_template",
                table: "meal_plans");

            migrationBuilder.DropColumn(
                name: "template_name",
                table: "meal_plans");

            migrationBuilder.AlterColumn<decimal>(
                name: "weight_kg",
                table: "meal_plans",
                type: "DECIMAL(6,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "DECIMAL(6,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "user_id",
                table: "meal_plans",
                type: "BIGINT",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "BIGINT",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "start_date",
                table: "meal_plans",
                type: "DATE",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "DATE",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "height_cm",
                table: "meal_plans",
                type: "DECIMAL(5,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "DECIMAL(5,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "end_date",
                table: "meal_plans",
                type: "DATE",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "DATE",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "bmi_value",
                table: "meal_plans",
                type: "DECIMAL(6,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "DECIMAL(6,2)",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_meal_plans_1",
                table: "meal_plans",
                sql: "DATEDIFF(DAY, start_date, end_date) = 6");

            migrationBuilder.AddCheckConstraint(
                name: "CK_meal_plans_2",
                table: "meal_plans",
                sql: "height_cm > 0 AND weight_kg > 0 AND bmi_value > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_meal_plans_6",
                table: "meal_plans",
                sql: "generation_source IN ('ai','manual')");
        }
    }
}
