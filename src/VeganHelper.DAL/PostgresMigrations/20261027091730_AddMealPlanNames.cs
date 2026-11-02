using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddMealPlanNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "meal_plans",
                type: "character varying(100)",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE meal_plans
                SET name = CASE
                    WHEN start_date IS NOT NULL THEN 'Week of ' || TO_CHAR(start_date, 'DD/MM/YYYY')
                    ELSE 'Weekly menu #' || id::text
                END
                WHERE is_template = FALSE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "name",
                table: "meal_plans");
        }
    }
}
