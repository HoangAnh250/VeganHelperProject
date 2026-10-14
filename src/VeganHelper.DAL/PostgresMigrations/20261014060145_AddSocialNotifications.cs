using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddSocialNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_notifications_type",
                table: "notifications");

            migrationBuilder.AddColumn<bool>(
                name: "notify_on_new_post",
                table: "user_follows",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "source_follow_id",
                table: "notifications",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "source_post_id",
                table: "notifications",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "post_follower_notifications",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    post_id = table.Column<long>(type: "bigint", nullable: false),
                    follow_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_follower_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_post_follower_notifications_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_post_follower_notifications_user_follows_follow_id",
                        column: x => x.follow_id,
                        principalTable: "user_follows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_post_follower_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_source_follow_id",
                table: "notifications",
                column: "source_follow_id");

            migrationBuilder.CreateIndex(name: "IX_notifications_source_post_id", table: "notifications", column: "source_post_id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_notifications_type",
                table: "notifications",
                sql: "type IN ('comment','comment_reply','post_like','new_follower','followed_post','post_review','meal_reminder')");

            migrationBuilder.CreateIndex(
                name: "IX_post_follower_notifications_follow_id_id",
                table: "post_follower_notifications",
                columns: new[] { "follow_id", "id" });

            migrationBuilder.Sql("""
                DO $$
                DECLARE sequence_name text; role_name text;
                BEGIN
                    ALTER TABLE public.post_follower_notifications ENABLE ROW LEVEL SECURITY;
                    REVOKE ALL ON TABLE public.post_follower_notifications FROM PUBLIC;
                    sequence_name := pg_get_serial_sequence('public.post_follower_notifications', 'id');
                    EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM PUBLIC', sequence_name);
                    FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated'] LOOP
                        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
                            EXECUTE format('REVOKE ALL ON TABLE public.post_follower_notifications FROM %I', role_name);
                            EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM %I', sequence_name, role_name);
                        END IF;
                    END LOOP;
                END $$;
                CREATE FUNCTION public.cancel_hidden_post_notifications() RETURNS trigger
                LANGUAGE plpgsql AS $fn$
                BEGIN
                    DELETE FROM public.post_follower_notifications WHERE post_id = NEW.id;
                    UPDATE public.notification_push_deliveries d SET state = 'cancelled'
                    FROM public.notifications n
                    WHERE n.id = d.notification_id AND n.source_post_id = NEW.id
                        AND n.type = 'followed_post' AND d.state IN ('pending', 'processing');
                    RETURN NEW;
                END $fn$;
                CREATE TRIGGER cancel_hidden_post_notifications
                AFTER UPDATE OF status, is_deleted ON public.posts FOR EACH ROW
                WHEN ((NEW.status <> 'published' OR NEW.is_deleted) AND
                      (OLD.status = 'published' AND NOT OLD.is_deleted))
                EXECUTE FUNCTION public.cancel_hidden_post_notifications();
                REVOKE ALL ON FUNCTION public.cancel_hidden_post_notifications() FROM PUBLIC;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_post_follower_notifications_post_id_follow_id",
                table: "post_follower_notifications",
                columns: new[] { "post_id", "follow_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_post_follower_notifications_user_id",
                table: "post_follower_notifications",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Never delete social notification history silently to make a rollback succeed.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM notifications WHERE type IN ('post_like','comment_reply','new_follower','followed_post')) THEN
                        RAISE EXCEPTION 'Cannot downgrade while social notification history exists. Export and review it first.';
                    END IF;
                END $$;
                """);
            migrationBuilder.Sql("DROP TRIGGER cancel_hidden_post_notifications ON public.posts; DROP FUNCTION public.cancel_hidden_post_notifications();");
            migrationBuilder.DropTable(
                name: "post_follower_notifications");

            migrationBuilder.DropIndex(name: "IX_notifications_source_post_id", table: "notifications");
            migrationBuilder.DropIndex(
                name: "IX_notifications_source_follow_id",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_notifications_type",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "notify_on_new_post",
                table: "user_follows");

            migrationBuilder.DropColumn(
                name: "source_follow_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "source_post_id",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "CK_notifications_type",
                table: "notifications",
                sql: "type IN ('comment','post_review','meal_reminder')");
        }
    }
}
