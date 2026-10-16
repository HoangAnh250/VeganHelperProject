using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class PublicationMigrationTests(Member2ApiFixture fixture)
{
    [Fact]
    public async Task Migration_PreservesLegacyVisibilityAndProtectsHistory()
    {
        var settings = new NpgsqlConnectionStringBuilder(fixture.Connection);
        Assert.Contains(settings.Host, new[] { "localhost", "127.0.0.1", "::1" });
        var name = "member2_test_migration_" + Guid.NewGuid().ToString("N");
        var adminSettings = new NpgsqlConnectionStringBuilder(fixture.Connection) { Database = "postgres" };
        await using (var admin = new NpgsqlConnection(adminSettings.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\" TEMPLATE template0 LOCALE_PROVIDER icu ICU_LOCALE 'und'", admin);
            await create.ExecuteNonQueryAsync();
        }
        settings.Database = name;
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(settings.ConnectionString).Options);
        await db.GetService<IMigrator>().MigrateAsync("20261007085728_AddPostModeration");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO users (id,username,email,password_hash,role_id,is_active) VALUES (900000,'migration_probe','migration@example.invalid','hash',1,true);
            INSERT INTO posts (author_id,title,post_type,status) VALUES
              (900000,'Public','recipe','published'), (900000,'Pending','community','pending_review'),
              (900000,'Hidden','video','hidden'), (900000,'Rejected','article','rejected');
            UPDATE posts SET content = 'Original content', "DifficultyLevel" = 'easy', prep_time_mins = 10,
                cooking_time_mins = 0, diet_type = 'vegan' WHERE title = 'Public';
            INSERT INTO ingredients (name,default_unit) VALUES ('Migration cucumber','g');
            INSERT INTO post_ingredients (post_id,ingredient_id,quantity,unit)
                SELECT p.id,i.id,100,'g' FROM posts p CROSS JOIN ingredients i WHERE p.title = 'Public';
            INSERT INTO post_steps (post_id,step_number,description)
                SELECT id,1,'Slice cucumber' FROM posts WHERE title = 'Public';
            INSERT INTO post_media (post_id,media_url,media_type,is_primary)
                SELECT id,'https://example.invalid/original.jpg','image',true FROM posts WHERE title = 'Public';
            UPDATE post_moderation_settings SET auto_publish_enabled = true;
            INSERT INTO post_moderation_scans (post_id,revision,state,attempts,next_attempt_at,findings_json,created_at)
              SELECT id,1,'safe',1,CURRENT_TIMESTAMP,'[]',CURRENT_TIMESTAMP FROM posts WHERE title = 'Public';
            """);
        await db.Database.MigrateAsync();
        await using var connection = new NpgsqlConnection(settings.ConnectionString); await connection.OpenAsync();
        await using var check = new NpgsqlCommand("""
            SELECT COUNT(*) FROM posts WHERE (status = 'published' AND moderation_hold = false)
                OR (status IN ('pending_review','hidden','rejected') AND moderation_hold = true)
            """, connection);
        Assert.Equal(4L, await check.ExecuteScalarAsync());
        await using var history = new NpgsqlCommand("SELECT COUNT(*) FROM post_moderation_assessments", connection);
        Assert.Equal(1L, await history.ExecuteScalarAsync());
        await using var mutate = new NpgsqlCommand("UPDATE post_moderation_assessments SET revision = 2", connection);
        await Assert.ThrowsAsync<PostgresException>(() => mutate.ExecuteNonQueryAsync());
        await using var truncate = new NpgsqlCommand("TRUNCATE post_moderation_assessments", connection);
        await Assert.ThrowsAsync<PostgresException>(() => truncate.ExecuteNonQueryAsync());
        await using var privileges = new NpgsqlCommand("SELECT has_table_privilege('anon','post_moderation_assessments','SELECT')", connection);
        Assert.Equal(false, await privileges.ExecuteScalarAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        await using var column = new NpgsqlCommand("SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='posts' AND column_name='post_type'", connection);
        Assert.Equal(0L, await column.ExecuteScalarAsync());
        var original = await db.Posts.Include(p => p.Media).Include(p => p.PostSteps)
            .Include(p => p.PostIngredients).ThenInclude(i => i.Ingredient).SingleAsync(p => p.Title == "Public");
        Assert.Equal("Original content", original.Content);
        Assert.Equal("easy", original.DifficultyLevel);
        Assert.Equal("vegan", original.DietType);
        Assert.Equal(10, original.PrepTimeMins); Assert.Equal(0, original.CookingTimeMins);
        Assert.Equal("https://example.invalid/original.jpg", Assert.Single(original.Media).MediaUrl);
        Assert.Equal("Slice cucumber", Assert.Single(original.PostSteps).Description);
        Assert.Equal("Migration cucumber", Assert.Single(original.PostIngredients).Ingredient!.Name);

        // Rollback restores a valid generic value, not the discarded labels.
        await db.GetService<IMigrator>().MigrateAsync("20261014060145_AddSocialNotifications");
        await using var fallback = new NpgsqlCommand("SELECT COUNT(*) FROM posts WHERE post_type='community'", connection);
        Assert.Equal(4L, await fallback.ExecuteScalarAsync());
        await db.Database.MigrateAsync();
        Assert.Equal(4, await db.Posts.CountAsync());
    }
}
