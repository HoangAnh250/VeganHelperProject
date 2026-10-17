using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddMealPlanTrialGenerations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_trial_generation",
                table: "meal_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_meal_plans_user_trial_created",
                table: "meal_plans",
                columns: new[] { "user_id", "is_trial_generation", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_meal_plans_user_trial_created",
                table: "meal_plans");

            migrationBuilder.DropColumn(
                name: "is_trial_generation",
                table: "meal_plans");
        }
    }
}
