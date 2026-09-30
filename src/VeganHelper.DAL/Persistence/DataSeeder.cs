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
                new Category { Name = "Nhà hàng sang trọng", Slug = "nha-hang-sang-trong", CategoryType = "shop", PostCategoryKind = null, IsActive = true },
                new Category { Name = "Quán ăn bình dân", Slug = "quan-an-binh-dan", CategoryType = "shop", PostCategoryKind = null, IsActive = true },
                new Category { Name = "Quán chay sân vườn", Slug = "quan-chay-san-vuon", CategoryType = "shop", PostCategoryKind = null, IsActive = true },
                new Category { Name = "Quán chay gia đình", Slug = "quan-chay-gia-dinh", CategoryType = "shop", PostCategoryKind = null, IsActive = true },
                new Category { Name = "Buffet chay", Slug = "buffet-chay", CategoryType = "shop", PostCategoryKind = null, IsActive = true },

                // Post Categories (Recipe)
                new Category { Name = "Món kho", Slug = "mon-kho", CategoryType = "post", PostCategoryKind = "recipe", IsActive = true },
                new Category { Name = "Món xào", Slug = "mon-xao", CategoryType = "post", PostCategoryKind = "recipe", IsActive = true },
                new Category { Name = "Món canh", Slug = "mon-canh", CategoryType = "post", PostCategoryKind = "recipe", IsActive = true },
                new Category { Name = "Món lẩu", Slug = "mon-lau", CategoryType = "post", PostCategoryKind = "recipe", IsActive = true },
                new Category { Name = "Bánh ngọt chay", Slug = "banh-ngot-chay", CategoryType = "post", PostCategoryKind = "recipe", IsActive = true },

                // Post Categories (Topic)
                new Category { Name = "Kiến thức dinh dưỡng", Slug = "kien-thuc-dinh-duong", CategoryType = "post", PostCategoryKind = "topic", IsActive = true },
                new Category { Name = "Tin tức ăn chay", Slug = "tin-tuc-an-chay", CategoryType = "post", PostCategoryKind = "topic", IsActive = true },
                new Category { Name = "Câu chuyện truyền cảm hứng", Slug = "cau-chuyen-truyen-cam-hung", CategoryType = "post", PostCategoryKind = "topic", IsActive = true },

                // Post Categories (Food)
                new Category { Name = "Món chay Việt", Slug = "mon-chay-viet", CategoryType = "post", PostCategoryKind = "food", IsActive = true },
                new Category { Name = "Món chay Âu", Slug = "mon-chay-au", CategoryType = "post", PostCategoryKind = "food", IsActive = true }
            };

            await context.Categories.AddRangeAsync(categories);
            await context.SaveChangesAsync();
        }
    }
}
