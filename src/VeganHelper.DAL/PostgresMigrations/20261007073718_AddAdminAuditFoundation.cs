using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VeganHelper.DAL.PostgresMigrations
{
    /// <inheritdoc />
    public partial class AddAdminAuditFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    admin_id = table.Column<long>(type: "bigint", nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    trace_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_audit_logs", x => x.id);
                    table.CheckConstraint("CK_admin_audit_logs_required", "length(trim(action)) > 0 AND length(trim(target_type)) > 0 AND length(trim(trace_id)) > 0");
                    table.ForeignKey(
                        name: "FK_admin_audit_logs_users_admin_id",
                        column: x => x.admin_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_logs_admin_id_created_at",
                table: "admin_audit_logs",
                columns: new[] { "admin_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_logs_created_at_id",
                table: "admin_audit_logs",
                columns: new[] { "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_logs_target_type_target_id",
                table: "admin_audit_logs",
                columns: new[] { "target_type", "target_id" });

            migrationBuilder.Sql("""
                ALTER TABLE public.admin_audit_logs ENABLE ROW LEVEL SECURITY;
                REVOKE ALL ON TABLE public.admin_audit_logs FROM PUBLIC;
                DO $$
                DECLARE role_name text; sequence_name text;
                BEGIN
                    sequence_name := pg_get_serial_sequence('public.admin_audit_logs', 'id');
                    EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM PUBLIC', sequence_name);
                    FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated'] LOOP
                        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
                            EXECUTE format('REVOKE ALL ON TABLE public.admin_audit_logs FROM %I', role_name);
                            EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM %I', sequence_name, role_name);
                        END IF;
                    END LOOP;
                END $$;

                CREATE FUNCTION public.reject_admin_audit_mutation() RETURNS trigger
                LANGUAGE plpgsql SET search_path = pg_catalog AS $$
                BEGIN
                    RAISE EXCEPTION 'Admin audit records are append-only.' USING ERRCODE = '42501';
                    RETURN NULL;
                END $$;
                REVOKE ALL ON FUNCTION public.reject_admin_audit_mutation() FROM PUBLIC;
                CREATE TRIGGER admin_audit_no_update_delete
                    BEFORE UPDATE OR DELETE ON public.admin_audit_logs
                    FOR EACH ROW EXECUTE FUNCTION public.reject_admin_audit_mutation();
                CREATE TRIGGER admin_audit_no_truncate
                    BEFORE TRUNCATE ON public.admin_audit_logs
                    FOR EACH STATEMENT EXECUTE FUNCTION public.reject_admin_audit_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_audit_logs");
            migrationBuilder.Sql("DROP FUNCTION public.reject_admin_audit_mutation();");
        }
    }
}
