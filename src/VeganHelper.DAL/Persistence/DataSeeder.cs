using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Persistence;

public static class DataSeeder
{
    public static async Task SeedDataAsync(AppDbContext context)
    {
        // 2. Seed Shops if empty
        if (!await context.Shops.AnyAsync())
        {
            var seedDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SeedData", "shops.postgresql.sql");
            if (File.Exists(seedDataPath))
            {
                var sql = await File.ReadAllTextAsync(seedDataPath);
                
                await context.Database.ExecuteSqlRawAsync(sql);
            }
            else
            {
                throw new FileNotFoundException("The PostgreSQL shop seed file is missing.", seedDataPath);
            }
        }

        // Insert missing categories by slug; never overwrite imported categories.
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

        var existingSlugs = await context.Categories.Select(c => c.Slug).ToListAsync();
        await context.Categories.AddRangeAsync(categories.Where(c => !existingSlugs.Contains(c.Slug)));
        await context.SaveChangesAsync();
    }
}
