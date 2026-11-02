using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddMealCookingSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "meal_cooking_steps",
                columns: table => new
                {
                    meal_id = table.Column<long>(type: "bigint", nullable: false),
                    step_number = table.Column<short>(type: "smallint", nullable: false),
                    instruction = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_cooking_steps", x => new { x.meal_id, x.step_number });
                    table.CheckConstraint("CK_meal_cooking_steps_instruction", "length(trim(instruction)) > 0");
                    table.CheckConstraint("CK_meal_cooking_steps_step_number", "step_number > 0");
                    table.ForeignKey(
                        name: "FK_meal_cooking_steps_meals",
                        column: x => x.meal_id,
                        principalTable: "meals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Browser clients use the authorized .NET API, not direct table access.
            migrationBuilder.Sql("""
                ALTER TABLE public.meal_cooking_steps ENABLE ROW LEVEL SECURITY;
                REVOKE ALL ON TABLE public.meal_cooking_steps FROM PUBLIC;
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
                        REVOKE ALL ON TABLE public.meal_cooking_steps FROM anon;
                    END IF;
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
                        REVOKE ALL ON TABLE public.meal_cooking_steps FROM authenticated;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meal_cooking_steps");
        }
    }
}
