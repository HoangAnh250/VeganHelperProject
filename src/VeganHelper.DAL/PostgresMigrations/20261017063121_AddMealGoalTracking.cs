using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddMealGoalTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "meal_plan_entries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MealPlanId = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    MealTime = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LoggedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_plan_entries", x => x.Id);
                    table.CheckConstraint("CK_entry_log", "(\"Status\" = 'unlogged') = (\"LoggedAt\" IS NULL)");
                    table.CheckConstraint("CK_entry_slot", "\"MealTime\" IN ('breakfast','lunch','dinner')");
                    table.CheckConstraint("CK_entry_status", "\"Status\" IN ('unlogged','followed','changed','skipped')");
                    table.ForeignKey(
                        name: "FK_meal_plan_entries_meal_plans_MealPlanId",
                        column: x => x.MealPlanId,
                        principalTable: "meal_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meal_plan_routines",
                columns: table => new
                {
                    MealPlanId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    RootPlanId = table.Column<long>(type: "bigint", nullable: false),
                    PreviousPlanId = table.Column<long>(type: "bigint", nullable: true),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetWeightKg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EligibilityConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RemindersEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    BreakfastTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    LunchTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    DinnerTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_plan_routines", x => x.MealPlanId);
                    table.CheckConstraint("CK_routine_goal", "(\"Mode\" = 'maintenance' AND \"TargetWeightKg\" IS NULL AND \"TargetDate\" IS NULL AND NOT \"RemindersEnabled\") OR (\"Mode\" = 'weight_goal' AND \"TargetWeightKg\" BETWEEN 30 AND 200 AND \"TargetDate\" IS NOT NULL AND \"EligibilityConfirmed\")");
                    table.CheckConstraint("CK_routine_mode", "\"Mode\" IN ('maintenance','weight_goal')");
                    table.CheckConstraint("CK_routine_state", "\"State\" IN ('active','continued','superseded')");
                    table.ForeignKey(
                        name: "FK_meal_plan_routines_meal_plans_MealPlanId",
                        column: x => x.MealPlanId,
                        principalTable: "meal_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_meal_plan_routines_meal_plans_PreviousPlanId",
                        column: x => x.PreviousPlanId,
                        principalTable: "meal_plans",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_meal_plan_routines_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_meal_plan_entries_DueAt",
                table: "meal_plan_entries",
                column: "DueAt");

            migrationBuilder.CreateIndex(
                name: "IX_meal_plan_entries_MealPlanId_Date_MealTime",
                table: "meal_plan_entries",
                columns: new[] { "MealPlanId", "Date", "MealTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meal_plan_routines_PreviousPlanId",
                table: "meal_plan_routines",
                column: "PreviousPlanId",
                unique: true,
                filter: "\"PreviousPlanId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_meal_plan_routines_UserId_State",
                table: "meal_plan_routines",
                columns: new[] { "UserId", "State" });
            migrationBuilder.Sql("""
                ALTER TABLE meal_plan_routines ENABLE ROW LEVEL SECURITY;
                ALTER TABLE meal_plan_entries ENABLE ROW LEVEL SECURITY;
                REVOKE ALL ON TABLE meal_plan_routines, meal_plan_entries FROM PUBLIC;
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='anon') THEN
                    REVOKE ALL ON TABLE meal_plan_routines, meal_plan_entries FROM anon;
                    REVOKE ALL ON SEQUENCE "meal_plan_entries_Id_seq" FROM anon;
                  END IF;
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname='authenticated') THEN
                    REVOKE ALL ON TABLE meal_plan_routines, meal_plan_entries FROM authenticated;
                    REVOKE ALL ON SEQUENCE "meal_plan_entries_Id_seq" FROM authenticated;
                  END IF;
                END $$;
                REVOKE ALL ON SEQUENCE "meal_plan_entries_Id_seq" FROM PUBLIC;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meal_plan_entries");

            migrationBuilder.DropTable(
                name: "meal_plan_routines");
        }
    }
}
