using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddNotificationsAndWebPush : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "browser_push_subscriptions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    endpoint_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    p256dh = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    auth = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_browser_push_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_browser_push_subscriptions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    event_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    target_url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.CheckConstraint("CK_notifications_read", "is_read = (read_at IS NOT NULL)");
                    table.CheckConstraint("CK_notifications_type", "type IN ('comment','post_review','meal_reminder')");
                    table.ForeignKey(
                        name: "FK_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_push_deliveries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    notification_id = table.Column<long>(type: "bigint", nullable: false),
                    subscription_id = table.Column<long>(type: "bigint", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    lease_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_push_deliveries", x => x.id);
                    table.CheckConstraint("CK_notification_delivery_attempts", "attempts BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_notification_delivery_state", "state IN ('pending','processing','sent','failed','expired','cancelled')");
                    table.ForeignKey(
                        name: "FK_notification_push_deliveries_browser_push_subscriptions_sub~",
                        column: x => x.subscription_id,
                        principalTable: "browser_push_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notification_push_deliveries_notifications_notification_id",
                        column: x => x.notification_id,
                        principalTable: "notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_browser_push_subscriptions_endpoint_hash",
                table: "browser_push_subscriptions",
                column: "endpoint_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_browser_push_subscriptions_user_id_is_active",
                table: "browser_push_subscriptions",
                columns: new[] { "user_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_push_deliveries_notification_id_subscription_id",
                table: "notification_push_deliveries",
                columns: new[] { "notification_id", "subscription_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_push_deliveries_state_next_attempt_at",
                table: "notification_push_deliveries",
                columns: new[] { "state", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_push_deliveries_subscription_id",
                table: "notification_push_deliveries",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id",
                table: "notifications",
                column: "user_id",
                filter: "is_read = false");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id_created_at_id",
                table: "notifications",
                columns: new[] { "user_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id_event_key",
                table: "notifications",
                columns: new[] { "user_id", "event_key" },
                unique: true);

            migrationBuilder.Sql("""
                DO $$
                DECLARE table_name text; sequence_name text; role_name text;
                BEGIN
                    FOREACH table_name IN ARRAY ARRAY['notifications', 'browser_push_subscriptions', 'notification_push_deliveries']
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
                name: "notification_push_deliveries");

            migrationBuilder.DropTable(
                name: "browser_push_subscriptions");

            migrationBuilder.DropTable(
                name: "notifications");
        }
    }
}
