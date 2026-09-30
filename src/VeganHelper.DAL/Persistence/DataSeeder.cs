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

        // 3. Seed Categories if empty
        if (!await context.Categories.AnyAsync())
        {
            var categories = new List<Category>
            {
                // Shop Categories
                new Category { Name = "Fine Dining", Slug = "fine-dining", CategoryType = "shop", PostCategoryKind = null, IsActive = true },
                new Category { Name = "Casual Dining", Slug = "casual-dining", CategoryType = "shop", PostCategoryKind = null, IsActive = true },
                new Category { Name = "Garden Restaurant", Slug = "garden-restaurant", CategoryType = "shop", PostCategoryKind = null, IsActive = true },
                new Category { Name = "Family Restaurant", Slug = "family-restaurant", CategoryType = "shop", PostCategoryKind = null, IsActive = true },
                new Category { Name = "Vegan Buffet", Slug = "vegan-buffet", CategoryType = "shop", PostCategoryKind = null, IsActive = true },

                // Post Categories (Recipe)
                new Category { Name = "Braised Dishes", Slug = "braised-dishes", CategoryType = "post", PostCategoryKind = "recipe", IsActive = true },
                new Category { Name = "Stir-fried Dishes", Slug = "stir-fried-dishes", CategoryType = "post", PostCategoryKind = "recipe", IsActive = true },
                new Category { Name = "Soups", Slug = "soups", CategoryType = "post", PostCategoryKind = "recipe", IsActive = true },
                new Category { Name = "Hot Pots", Slug = "hot-pots", CategoryType = "post", PostCategoryKind = "recipe", IsActive = true },
                new Category { Name = "Vegan Pastries", Slug = "vegan-pastries", CategoryType = "post", PostCategoryKind = "recipe", IsActive = true },

                // Post Categories (Topic)
                new Category { Name = "Nutritional Knowledge", Slug = "nutritional-knowledge", CategoryType = "post", PostCategoryKind = "topic", IsActive = true },
                new Category { Name = "Vegan News", Slug = "vegan-news", CategoryType = "post", PostCategoryKind = "topic", IsActive = true },
                new Category { Name = "Inspiring Stories", Slug = "inspiring-stories", CategoryType = "post", PostCategoryKind = "topic", IsActive = true },

                // Post Categories (Food)
                new Category { Name = "Vietnamese Vegan Foods", Slug = "vietnamese-vegan-foods", CategoryType = "post", PostCategoryKind = "food", IsActive = true },
                new Category { Name = "Western Vegan Foods", Slug = "western-vegan-foods", CategoryType = "post", PostCategoryKind = "food", IsActive = true }
            };

            await context.Categories.AddRangeAsync(categories);
            await context.SaveChangesAsync();
        }
    }
}
