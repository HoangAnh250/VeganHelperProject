using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Persistence;

public static class DataSeeder
{
    public static async Task SeedDataAsync(AppDbContext context)
    {
        // 1. Ensure DB is created and migrated
        await context.Database.MigrateAsync();

        // 2. Seed Shops if empty
        if (!await context.Shops.AnyAsync())
        {
            var seedDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SeedData", "quan_chay_hcm_100.sql");
            if (File.Exists(seedDataPath))
            {
                var sql = await File.ReadAllTextAsync(seedDataPath);
                
                // Note: The SQL file has been modified to include SET IDENTITY_INSERT shops ON/OFF
                await context.Database.ExecuteSqlRawAsync(sql);
            }
        }
    }
}
