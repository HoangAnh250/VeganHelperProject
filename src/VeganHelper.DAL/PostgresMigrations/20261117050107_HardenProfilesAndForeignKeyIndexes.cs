using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class HardenProfilesAndForeignKeyIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_legacy_allergy_alias",
                table: "ingredients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "avatar_cleanup",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_avatar_cleanup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "custom_user_allergies",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_user_allergies", x => new { x.UserId, x.Name });
                    table.ForeignKey(
                        name: "FK_custom_user_allergies_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                SET LOCAL lock_timeout = '5s';
                ALTER TABLE public.custom_user_allergies ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public.avatar_cleanup ENABLE ROW LEVEL SECURITY;
                REVOKE ALL ON TABLE public.custom_user_allergies, public.avatar_cleanup FROM PUBLIC;
                DO $$ DECLARE role_name text;
                BEGIN
                    FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated'] LOOP
                        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
                            EXECUTE format('REVOKE ALL ON TABLE public.custom_user_allergies, public.avatar_cleanup FROM %I', role_name);
                        END IF;
                    END LOOP;
                END $$;

                INSERT INTO public.custom_user_allergies ("UserId", "Name")
                SELECT DISTINCT a.user_id, i.name
                FROM public.user_allergies a JOIN public.ingredients i ON i.id = a.ingredient_id
                WHERE a.is_custom
                ON CONFLICT DO NOTHING;

                UPDATE public.ingredients i
                SET is_legacy_allergy_alias = true
                WHERE EXISTS (SELECT 1 FROM public.user_allergies a WHERE a.ingredient_id = i.id AND a.is_custom)
                  AND NOT EXISTS (SELECT 1 FROM public.user_allergies a WHERE a.ingredient_id = i.id AND NOT a.is_custom)
                  AND NOT EXISTS (SELECT 1 FROM public.post_ingredients p WHERE p.ingredient_id = i.id)
                  AND NOT EXISTS (SELECT 1 FROM public.meal_ingredients m WHERE m.ingredient_id = i.id)
                  AND i.calories_per_100g IS NULL;

                UPDATE public.user_profiles
                SET current_bmi = round(weight_kg / power(height_cm / 100, 2), 2)
                WHERE weight_kg BETWEEN 30 AND 200 AND height_cm BETWEEN 100 AND 250
                  AND current_bmi IS DISTINCT FROM round(weight_kg / power(height_cm / 100, 2), 2);

                CREATE INDEX IF NOT EXISTS "IX_meal_plan_routines_previous_plan_fk"
                    ON public.meal_plan_routines ("PreviousPlanId");
                CREATE INDEX IF NOT EXISTS "IX_flags_comment_id"
                    ON public.flags (comment_id);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_avatar_cleanup_NextAttemptAt",
                table: "avatar_cleanup",
                column: "NextAttemptAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "avatar_cleanup");

            migrationBuilder.DropTable(
                name: "custom_user_allergies");

            migrationBuilder.DropIndex(
                name: "IX_meal_plan_routines_previous_plan_fk",
                table: "meal_plan_routines");

            migrationBuilder.DropIndex(
                name: "IX_flags_comment_id",
                table: "flags");

            migrationBuilder.DropColumn(
                name: "is_legacy_allergy_alias",
                table: "ingredients");
        }
    }
}
