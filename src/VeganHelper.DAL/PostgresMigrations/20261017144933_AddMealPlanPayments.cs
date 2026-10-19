using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddMealPlanPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "meal_plan_payment_order_code_seq",
                startValue: 1000000000L);

            migrationBuilder.CreateTable(
                name: "meal_plan_packages",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", nullable: false),
                    duration_months = table.Column<int>(type: "integer", nullable: false),
                    amount_vnd = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_plan_packages", x => x.id);
                    table.CheckConstraint("CK_meal_plan_packages_amount", "amount_vnd > 0");
                    table.CheckConstraint("CK_meal_plan_packages_duration", "duration_months IN (1, 6, 12)");
                });

            migrationBuilder.CreateTable(
                name: "meal_plan_payment_orders",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    meal_plan_package_id = table.Column<int>(type: "integer", nullable: false),
                    order_code = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('meal_plan_payment_order_code_seq')"),
                    package_code_snapshot = table.Column<string>(type: "character varying(40)", nullable: false),
                    package_name_snapshot = table.Column<string>(type: "character varying(100)", nullable: false),
                    duration_months_snapshot = table.Column<int>(type: "integer", nullable: false),
                    amount_vnd = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", nullable: false, defaultValue: "pending"),
                    payment_link_id = table.Column<string>(type: "character varying(100)", nullable: true),
                    checkout_url = table.Column<string>(type: "character varying(2000)", nullable: true),
                    provider_reference = table.Column<string>(type: "character varying(100)", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_plan_payment_orders", x => x.id);
                    table.CheckConstraint("CK_meal_plan_payment_orders_amount", "amount_vnd > 0");
                    table.CheckConstraint("CK_meal_plan_payment_orders_duration", "duration_months_snapshot IN (1, 6, 12)");
                    table.CheckConstraint("CK_meal_plan_payment_orders_status", "status IN ('pending','paid','failed')");
                    table.ForeignKey(
                        name: "FK_meal_plan_payment_orders_package",
                        column: x => x.meal_plan_package_id,
                        principalTable: "meal_plan_packages",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_meal_plan_payment_orders_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_meal_plan_access",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    meal_plan_package_id = table.Column<int>(type: "integer", nullable: false),
                    payment_order_id = table.Column<long>(type: "bigint", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_meal_plan_access", x => x.id);
                    table.CheckConstraint("CK_user_meal_plan_access_period", "expires_at > starts_at");
                    table.ForeignKey(
                        name: "FK_user_meal_plan_access_package",
                        column: x => x.meal_plan_package_id,
                        principalTable: "meal_plan_packages",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_user_meal_plan_access_payment_order",
                        column: x => x.payment_order_id,
                        principalTable: "meal_plan_payment_orders",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_user_meal_plan_access_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.InsertData(
                table: "meal_plan_packages",
                columns: new[] { "id", "amount_vnd", "code", "created_at", "duration_months", "is_active", "name" },
                values: new object[,]
                {
                    { 1, 100000, "monthly", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, "1 month" },
                    { 2, 500000, "half_year", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Utc), 6, true, "6 months" },
                    { 3, 1000000, "yearly", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Utc), 12, true, "12 months" }
                });

            migrationBuilder.CreateIndex(
                name: "UQ_meal_plan_packages_code",
                table: "meal_plan_packages",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meal_plan_payment_orders_meal_plan_package_id",
                table: "meal_plan_payment_orders",
                column: "meal_plan_package_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_plan_payment_orders_user_created",
                table: "meal_plan_payment_orders",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "UQ_meal_plan_payment_orders_order_code",
                table: "meal_plan_payment_orders",
                column: "order_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_meal_plan_payment_orders_payment_link",
                table: "meal_plan_payment_orders",
                column: "payment_link_id",
                unique: true,
                filter: "payment_link_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_meal_plan_access_meal_plan_package_id",
                table: "user_meal_plan_access",
                column: "meal_plan_package_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_meal_plan_access_user_expires",
                table: "user_meal_plan_access",
                columns: new[] { "user_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_user_meal_plan_access_user_period",
                table: "user_meal_plan_access",
                columns: new[] { "user_id", "starts_at", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "UQ_user_meal_plan_access_payment_order",
                table: "user_meal_plan_access",
                column: "payment_order_id",
                unique: true);

            // Payment records are accessible only through the Backend. Supabase's
            // browser roles must not read or mutate payment/order/access data.
            migrationBuilder.Sql("""
                ALTER TABLE public.meal_plan_packages ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public.meal_plan_payment_orders ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public.user_meal_plan_access ENABLE ROW LEVEL SECURITY;

                REVOKE ALL ON TABLE public.meal_plan_packages FROM PUBLIC;
                REVOKE ALL ON TABLE public.meal_plan_payment_orders FROM PUBLIC;
                REVOKE ALL ON TABLE public.user_meal_plan_access FROM PUBLIC;
                REVOKE ALL ON SEQUENCE public.meal_plan_payment_order_code_seq FROM PUBLIC;

                DO $$
                DECLARE role_name text;
                BEGIN
                    FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated'] LOOP
                        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
                            EXECUTE format('REVOKE ALL ON TABLE public.meal_plan_packages FROM %I', role_name);
                            EXECUTE format('REVOKE ALL ON TABLE public.meal_plan_payment_orders FROM %I', role_name);
                            EXECUTE format('REVOKE ALL ON TABLE public.user_meal_plan_access FROM %I', role_name);
                            EXECUTE format('REVOKE ALL ON SEQUENCE public.meal_plan_payment_order_code_seq FROM %I', role_name);
                        END IF;
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_meal_plan_access");

            migrationBuilder.DropTable(
                name: "meal_plan_payment_orders");

            migrationBuilder.DropTable(
                name: "meal_plan_packages");

            migrationBuilder.DropSequence(
                name: "meal_plan_payment_order_code_seq");
        }
    }
}
