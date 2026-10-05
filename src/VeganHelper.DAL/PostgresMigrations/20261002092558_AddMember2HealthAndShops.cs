using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddMember2HealthAndShops : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_custom",
                table: "user_allergies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "contact_phone",
                table: "shops",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "shops",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "opening_hours",
                table: "shops",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "rating",
                table: "shops",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "shop_media",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    shop_id = table.Column<long>(type: "bigint", nullable: false),
                    media_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_media", x => x.id);
                    table.CheckConstraint("CK_shop_media_order", "display_order >= 0");
                    table.ForeignKey(
                        name: "FK_shop_media_shops_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shop_menu_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    shop_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_menu_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_shop_menu_items_shops_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shop_opening_periods",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    shop_id = table.Column<long>(type: "bigint", nullable: false),
                    day_of_week = table.Column<byte>(type: "smallint", nullable: false),
                    opens_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    closes_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    is_closed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_opening_periods", x => x.id);
                    table.CheckConstraint("CK_shop_opening_periods_day", "day_of_week BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "FK_shop_opening_periods_shops_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shops_member2_location",
                table: "shops",
                columns: new[] { "is_approved", "is_deleted", "latitude", "longitude" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_shops_member2_rating",
                table: "shops",
                sql: "rating IS NULL OR rating BETWEEN 0 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_shop_media_shop_id_display_order",
                table: "shop_media",
                columns: new[] { "shop_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "IX_shop_menu_items_shop_id_name",
                table: "shop_menu_items",
                columns: new[] { "shop_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shop_opening_periods_shop_id_day_of_week",
                table: "shop_opening_periods",
                columns: new[] { "shop_id", "day_of_week" });

            // Match SharedDatabaseSafety: the .NET backend owns access, not the Supabase Data API.
            migrationBuilder.Sql("""
                DO $$
                DECLARE table_name text; sequence_name text; role_name text;
                BEGIN
                    FOREACH table_name IN ARRAY ARRAY['shop_media', 'shop_menu_items', 'shop_opening_periods']
                    LOOP
                        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', table_name);
                        EXECUTE format('REVOKE ALL ON TABLE public.%I FROM PUBLIC', table_name);
                        sequence_name := pg_get_serial_sequence(format('public.%I', table_name), 'id');
                        EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM PUBLIC', sequence_name);
                        FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated']
                        LOOP
                            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
                                EXECUTE format('REVOKE ALL ON TABLE public.%I FROM %I', table_name, role_name);
                                EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM %I', sequence_name, role_name);
                            END IF;
                        END LOOP;
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shop_media");

            migrationBuilder.DropTable(
                name: "shop_menu_items");

            migrationBuilder.DropTable(
                name: "shop_opening_periods");

            migrationBuilder.DropIndex(
                name: "IX_shops_member2_location",
                table: "shops");

            migrationBuilder.DropCheckConstraint(
                name: "CK_shops_member2_rating",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "is_custom",
                table: "user_allergies");

            migrationBuilder.DropColumn(
                name: "contact_phone",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "description",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "opening_hours",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "rating",
                table: "shops");
        }
    }
}
