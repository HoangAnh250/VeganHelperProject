using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddMealSuitableTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "meal_suitable_times",
                columns: table => new
                {
                    meal_id = table.Column<long>(type: "bigint", nullable: false),
                    meal_time = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_suitable_times", x => new { x.meal_id, x.meal_time });
                    table.CheckConstraint("CK_meal_suitable_times_time", "meal_time IN ('breakfast', 'lunch', 'dinner')");
                    table.ForeignKey(
                        name: "FK_meal_suitable_times_meals_meal_id",
                        column: x => x.meal_id,
                        principalTable: "meals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_meal_suitable_times_meal_time_meal_id",
                table: "meal_suitable_times",
                columns: new[] { "meal_time", "meal_id" });

            // Match the existing boundary: browser clients use the authorized .NET API.
            migrationBuilder.Sql("""
                ALTER TABLE public.meal_suitable_times ENABLE ROW LEVEL SECURITY;
                REVOKE ALL ON TABLE public.meal_suitable_times FROM PUBLIC;
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
                        REVOKE ALL ON TABLE public.meal_suitable_times FROM anon;
                    END IF;
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
                        REVOKE ALL ON TABLE public.meal_suitable_times FROM authenticated;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meal_suitable_times");
        }
    }
}
