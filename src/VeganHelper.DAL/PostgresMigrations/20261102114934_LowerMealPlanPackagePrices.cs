using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class LowerMealPlanPackagePrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "meal_plan_packages",
                keyColumn: "id",
                keyValue: 1,
                column: "amount_vnd",
                value: 1000);

            migrationBuilder.UpdateData(
                table: "meal_plan_packages",
                keyColumn: "id",
                keyValue: 2,
                column: "amount_vnd",
                value: 5000);

            migrationBuilder.UpdateData(
                table: "meal_plan_packages",
                keyColumn: "id",
                keyValue: 3,
                column: "amount_vnd",
                value: 10000);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "meal_plan_packages",
                keyColumn: "id",
                keyValue: 1,
                column: "amount_vnd",
                value: 100000);

            migrationBuilder.UpdateData(
                table: "meal_plan_packages",
                keyColumn: "id",
                keyValue: 2,
                column: "amount_vnd",
                value: 500000);

            migrationBuilder.UpdateData(
                table: "meal_plan_packages",
                keyColumn: "id",
                keyValue: 3,
                column: "amount_vnd",
                value: 1000000);
        }
    }
}
