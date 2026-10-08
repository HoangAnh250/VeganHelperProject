using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class ModerationMigrationTests(Member2ApiFixture fixture)
{
    [Fact]
    public async Task AddPostModeration_WithLegacyPostsAndAiFlag_PreservesDataAndBackfillsRevisionsAndQueue()
    {
        var parent = new NpgsqlConnectionStringBuilder(fixture.Connection);
        if (parent.Host is not ("localhost" or "127.0.0.1" or "::1") ||
            parent.Database is null || !parent.Database.StartsWith("member2_test", StringComparison.Ordinal))
            throw new InvalidOperationException("Migration tests require an isolated local Member2 test database.");

        // PostgreSQL identifiers are limited to 63 bytes; retain a 112-bit unique suffix.
        var databaseName = "member2_test_moderation_migration_" + Guid.NewGuid().ToString("N")[..28];
        var maintenanceConfiguration = new NpgsqlConnectionStringBuilder(parent.ConnectionString)
        { Database = "postgres", Pooling = false };
        await using var maintenance = new NpgsqlConnection(maintenanceConfiguration.ConnectionString);
        await maintenance.OpenAsync();
        using var identifiers = new NpgsqlCommandBuilder();
        var quotedDatabaseName = identifiers.QuoteIdentifier(databaseName);
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {quotedDatabaseName} TEMPLATE template0 ENCODING 'UTF8' LOCALE_PROVIDER icu ICU_LOCALE 'en-US'", maintenance))
            await create.ExecuteNonQueryAsync();

        try
        {
            var isolatedConfiguration = new NpgsqlConnectionStringBuilder(parent.ConnectionString)
            { Database = databaseName, Pooling = false };
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(isolatedConfiguration.ConnectionString).Options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261007080739_AddAdminMemberBans");

            await using var legacy = new NpgsqlConnection(isolatedConfiguration.ConnectionString);
            await legacy.OpenAsync();
            // Use old-schema SQL: the current EF Post and Flag models require columns not added yet.
            var authorId = await InsertAsync(legacy, """
                INSERT INTO users (username, email, role_id)
                VALUES ('legacy_moderation', 'legacy_moderation@example.invalid', 1) RETURNING id
                """);
            var pendingPostId = await InsertAsync(legacy, """
                INSERT INTO posts (author_id, post_type, title, content, status)
                VALUES (@author, 'community', 'Legacy pending title', 'Legacy pending content', 'pending_review') RETURNING id
                """, ("author", authorId));
            var publishedPostId = await InsertAsync(legacy, """
                INSERT INTO posts (author_id, post_type, title, content, status)
                VALUES (@author, 'article', 'Legacy published title', 'Legacy published content', 'published') RETURNING id
                """, ("author", authorId));
            var flagId = await InsertAsync(legacy, """
                INSERT INTO flags (post_id, reason, status, source_type, ai_model_name)
                VALUES (@post, 'Legacy AI evidence requiring human review', 'pending', 'ai', 'legacy-model') RETURNING id
                """, ("post", pendingPostId));

            await migrator.MigrateAsync();

            var author = await db.Users.AsNoTracking().SingleAsync();
            Assert.Equal(authorId, author.Id);
            Assert.Equal("legacy_moderation", author.Username);
            Assert.Equal("legacy_moderation@example.invalid", author.Email);
            var posts = await db.Posts.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
            Assert.Equal(2, posts.Count);
            var pending = Assert.Single(posts, p => p.Id == pendingPostId);
            Assert.Equal("Legacy pending title", pending.Title);
            Assert.Equal("Legacy pending content", pending.Content);
            Assert.Equal("pending_review", pending.Status);
            Assert.Equal(1, pending.ContentRevision);
            var published = Assert.Single(posts, p => p.Id == publishedPostId);
            Assert.Equal("Legacy published title", published.Title);
            Assert.Equal("Legacy published content", published.Content);
            Assert.Equal("published", published.Status);
            Assert.Equal(1, published.ContentRevision);

            var flag = await db.Flags.AsNoTracking().SingleAsync();
            Assert.Equal(flagId, flag.Id);
            Assert.Equal(pendingPostId, flag.PostId);
            Assert.Equal(1, flag.PostRevision);
            Assert.Equal("Legacy AI evidence requiring human review", flag.Reason);
            Assert.Equal("pending", flag.Status);
            Assert.Equal("ai", flag.SourceType);
            Assert.Equal("legacy-model", flag.AiModelName);
            Assert.Null(flag.ModerationScanId);

            var scan = await db.Set<PostModerationScan>().AsNoTracking().SingleAsync();
            Assert.Equal(pendingPostId, scan.PostId);
            Assert.Equal(1, scan.Revision);
            Assert.Equal("queued", scan.State);
            Assert.Equal(0, scan.Attempts);
            Assert.False(await db.Set<PostModerationScan>().AnyAsync(s => s.PostId == publishedPostId));
            var settings = await db.Set<PostModerationSettings>().AsNoTracking().SingleAsync();
            Assert.False(settings.AutoPublishEnabled);
            Assert.Equal(1, settings.Version);
            Assert.Empty(await db.Set<PostModerationDecision>().ToListAsync());
        }
        finally
        {
            // This exact name was generated and created above; never drop the fixture database.
            await using var drop = new NpgsqlCommand($"DROP DATABASE {quotedDatabaseName} WITH (FORCE)", maintenance);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<long> InsertAsync(NpgsqlConnection connection, string sql,
        params (string Name, long Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Legacy row was not inserted."));
    }
}
