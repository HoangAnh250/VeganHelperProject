using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.PostgresMigrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261001160000_SharedDatabaseSafety")]
public sealed class SharedDatabaseSafety : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // RLS protects application data from Supabase's browser-facing Data API.
        // The trusted .NET database owner still enforces API authorization in the backend.
        migrationBuilder.Sql("""
            DO $$
            DECLARE item record;
            BEGIN
                FOR item IN
                    SELECT tablename FROM pg_tables
                    WHERE schemaname = 'public' AND tablename = ANY (ARRAY[
                        'ai_usage','bmi_history','categories','chat_messages','chat_sessions','comments',
                        'email_verification_tokens','flags','ingredients','meal_ingredients','meal_plan_schedule',
                        'meal_plans','meals','password_reset_tokens','post_categories','post_embeddings',
                        'post_ingredients','post_likes','post_media','post_steps','post_summaries','posts',
                        'refresh_tokens','roles','saved_posts','shop_categories','shops','user_allergies',
                        'user_available_ingredients','user_identities','user_profiles','users'])
                LOOP
                    EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', item.tablename);
                    EXECUTE format('REVOKE ALL ON TABLE public.%I FROM PUBLIC', item.tablename);
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
                        EXECUTE format('REVOKE ALL ON TABLE public.%I FROM anon', item.tablename);
                    END IF;
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
                        EXECUTE format('REVOKE ALL ON TABLE public.%I FROM authenticated', item.tablename);
                    END IF;
                END LOOP;
            END $$;
            """);
        migrationBuilder.Sql("""
            DO $$
            DECLARE item record;
            BEGIN
                FOR item IN
                    SELECT c.conname, t.relname FROM pg_constraint c
                    JOIN pg_class t ON c.conrelid = t.oid
                    JOIN pg_namespace n ON t.relnamespace = n.oid
                    WHERE c.contype = 'f' AND n.nspname = 'public'
                      AND t.relname = ANY (ARRAY[
                        'ai_usage','bmi_history','categories','chat_messages','chat_sessions','comments',
                        'email_verification_tokens','flags','ingredients','meal_ingredients','meal_plan_schedule',
                        'meal_plans','meals','password_reset_tokens','post_categories','post_embeddings',
                        'post_ingredients','post_likes','post_media','post_steps','post_summaries','posts',
                        'refresh_tokens','roles','saved_posts','shop_categories','shops','user_allergies',
                        'user_available_ingredients','user_identities','user_profiles','users'])
                LOOP
                    EXECUTE format('ALTER TABLE public.%I ALTER CONSTRAINT %I DEFERRABLE INITIALLY IMMEDIATE', item.relname, item.conname);
                END LOOP;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Do not automatically remove protection from a shared database.
        throw new NotSupportedException("Restore the pre-migration backup or review an explicit rollback migration.");
    }
}
