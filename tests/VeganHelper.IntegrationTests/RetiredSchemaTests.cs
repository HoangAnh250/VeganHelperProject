using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class RetiredSchemaTests(Member2ApiFixture fixture)
{
    [Fact]
    public async Task LatestSchema_RemovesOnlyRetiredObjectsAndKeepsModerationScanLinks()
    {
        await using var connection = new NpgsqlConnection(fixture.Connection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT to_regclass('public.post_embeddings') IS NULL
               AND to_regclass('public.user_available_ingredients') IS NULL
               AND NOT EXISTS (
                   SELECT 1 FROM information_schema.columns
                   WHERE table_schema = 'public'
                     AND ((table_name = 'post_media' AND column_name = 'cloud_public_id')
                       OR (table_name = 'post_moderation_decisions' AND column_name = 'scan_id')))
               AND EXISTS (
                   SELECT 1 FROM information_schema.columns
                   WHERE table_schema = 'public' AND table_name = 'flags' AND column_name = 'moderation_scan_id')
               AND to_regclass('public.post_moderation_scans') IS NOT NULL
               AND to_regclass('public.shops') IS NOT NULL;
            """, connection);
        Assert.True((bool)(await command.ExecuteScalarAsync())!);
        await using var db = fixture.Context();
        // Materializing mapped entities catches leftover EF references to the dropped columns.
        await db.PostMedia.AsNoTracking().Take(1).ToListAsync();
        await db.Set<VeganHelper.DAL.Entities.PostModerationDecision>().AsNoTracking().Take(1).ToListAsync();
    }
}
