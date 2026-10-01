using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Persistence;

public static class PostSeeder
{
    public static async Task SeedPostsAsync(AppDbContext context)
    {
        // 1. Ensure a seeder author exists
        var seederUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "seeder@veganhelper.com");
        if (seederUser == null)
        {
            seederUser = new User
            {
                Username = "post_seeder",
                Email = "seeder@veganhelper.com",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                RoleId = 2 // Assuming 2 is a generic or author role
            };
            context.Users.Add(seederUser);
            await context.SaveChangesAsync();

            var userProfile = new UserProfile
            {
                UserId = seederUser.Id,
                DisplayName = "Vegan Helper Chef",
                DietType = "vegan",
                UpdatedAt = DateTime.UtcNow
            };
            context.UserProfiles.Add(userProfile);
            await context.SaveChangesAsync();
        }

        // 3. Define realistic vegan recipes with images
        var recipeData = new[]
        {
            new { Title = "Vegan Stir-Fried Tofu with Mushrooms", Slug = "stir-fried-dishes", Img = "https://images.unsplash.com/photo-1546069901-ba9599a7e63c?auto=format&fit=crop&w=800&q=80" },
            new { Title = "Vietnamese Vegan Pho", Slug = "soups", Img = "https://images.unsplash.com/photo-1582878826629-29b7ad1cb431?auto=format&fit=crop&w=800&q=80" },
            new { Title = "Vegetarian Mushroom Hot Pot", Slug = "hot-pots", Img = "https://images.unsplash.com/photo-1555126634-323283e090fa?auto=format&fit=crop&w=800&q=80" },
            new { Title = "Crispy Tofu with Lemongrass", Slug = "braised-dishes", Img = "https://images.unsplash.com/photo-1511690656952-34342bb7c2f2?auto=format&fit=crop&w=800&q=80" },
            new { Title = "Vegan Pumpkin Soup", Slug = "soups", Img = "https://images.unsplash.com/photo-1476718406336-bb5a9690ee2a?auto=format&fit=crop&w=800&q=80" },
            new { Title = "Fresh Vegetable Spring Rolls", Slug = "stir-fried-dishes", Img = "https://images.unsplash.com/photo-1512621776951-a57141f2eefd?auto=format&fit=crop&w=800&q=80" },
            new { Title = "Plant-Based Banana Cake", Slug = "vegan-pastries", Img = "https://images.unsplash.com/photo-1576618148400-f54bed99fcfd?auto=format&fit=crop&w=800&q=80" },
            new { Title = "Braised Eggplant with Soy Sauce", Slug = "braised-dishes", Img = "https://images.unsplash.com/photo-1564834724105-918b73d1b9e0?auto=format&fit=crop&w=800&q=80" },
            new { Title = "Stir-Fried Bok Choy and Garlic", Slug = "stir-fried-dishes", Img = "https://images.unsplash.com/photo-1504674900247-0877df9cc836?auto=format&fit=crop&w=800&q=80" },
            new { Title = "Vegan Tom Yum Soup", Slug = "soups", Img = "https://images.unsplash.com/photo-1548943487-a2e4b43b4850?auto=format&fit=crop&w=800&q=80" },
            new { Title = "Mango Sticky Rice Dessert", Slug = "vegan-pastries", Img = "https://images.unsplash.com/photo-1563805042-7684c8a9e9cb?auto=format&fit=crop&w=800&q=80" },
            new { Title = "Vegan Coconut Curry", Slug = "braised-dishes", Img = "https://images.unsplash.com/photo-1604908176997-125f25cc6f3d?auto=format&fit=crop&w=800&q=80" },
            new { Title = "Vegetable Dumplings", Slug = "stir-fried-dishes", Img = "https://images.unsplash.com/photo-1496116218417-1a781b1c416c?auto=format&fit=crop&w=800&q=80" },
            new { Title = "Vegan Chocolate Brownies", Slug = "vegan-pastries", Img = "https://images.unsplash.com/photo-1606313564200-e75d5e30476c?auto=format&fit=crop&w=800&q=80" },
            new { Title = "Spicy Vegan Mapo Tofu", Slug = "braised-dishes", Img = "https://images.unsplash.com/photo-1552611052-33e04de081de?auto=format&fit=crop&w=800&q=80" },
            new { Title = "Creamy Mushroom Soup", Slug = "soups", Img = "https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=800&q=80" },
            new { Title = "Tofu and Vegetable Skewers", Slug = "stir-fried-dishes", Img = "https://images.unsplash.com/photo-1555939594-58d7cb561ad1?auto=format&fit=crop&w=800&q=80" },
            new { Title = "Matcha Vegan Cheesecake", Slug = "vegan-pastries", Img = "https://images.unsplash.com/photo-1550617931-e17a7b70dce2?auto=format&fit=crop&w=800&q=80" },
            new { Title = "Vegan Ramen", Slug = "soups", Img = "https://images.unsplash.com/photo-1557872943-16a5ac26437e?auto=format&fit=crop&w=800&q=80" },
            new { Title = "Braised Jackfruit with Spices", Slug = "braised-dishes", Img = "https://images.unsplash.com/photo-1512152272829-e3139592d56f?auto=format&fit=crop&w=800&q=80" }
        };

        // 2. Fetch existing seeded posts
        var existingPosts = await context.Posts
            .Include(p => p.Media)
            .Include(p => p.PostCategories)
            .Where(p => p.AuthorId == seederUser.Id && p.Title.StartsWith("[Demo]"))
            .ToListAsync();

        // 4. Fetch valid categories for recipes
        var categories = await context.Categories
            .Where(c => c.IsActive && c.CategoryType == "post" && c.PostCategoryKind == "recipe")
            .ToListAsync();

        if (categories.Count == 0)
        {
            Console.WriteLine("No recipe categories found. Please run the standard DataSeeder first.");
            return;
        }

        var random = new Random();
        var posts = new List<Post>();

        for (int i = 0; i < recipeData.Length; i++)
        {
            var data = recipeData[i];
            var category = categories.FirstOrDefault(c => c.Slug == data.Slug) ?? categories.First();
            string newTitle = $"[Demo] {data.Title}";

            // Update if exists
            if (i < existingPosts.Count)
            {
                var post = existingPosts[i];
                post.Title = newTitle;
                post.Content = $"This is a beautifully crafted vegan recipe for {data.Title}. It is rich in flavor and completely plant-based. Perfect for family dinners or a healthy meal prep.";
                
                // Update media
                var media = post.Media.FirstOrDefault();
                if (media != null)
                {
                    media.MediaUrl = data.Img;
                }
                
                // Update category
                var postCat = post.PostCategories.FirstOrDefault();
                if (postCat != null && postCat.CategoryId != category.Id)
                {
                    context.Set<PostCategory>().Remove(postCat);
                    post.PostCategories.Add(new PostCategory { CategoryId = category.Id });
                }
            }
            else
            {
                // Create new if there are not enough existing posts
                var post = new Post
                {
                    AuthorId = seederUser.Id,
                    PostType = "recipe",
                    Title = newTitle,
                    Content = $"This is a beautifully crafted vegan recipe for {data.Title}. It is rich in flavor and completely plant-based. Perfect for family dinners or a healthy meal prep.",
                    DifficultyLevel = i % 3 == 0 ? "Hard" : i % 2 == 0 ? "Medium" : "Easy",
                    MealType = "dinner",
                    PrepTimeMins = random.Next(10, 30),
                    CookingTimeMins = random.Next(15, 60),
                    Servings = random.Next(2, 6),
                    CaloriesPerServing = random.Next(200, 600),
                    DietType = "vegan",
                    IngredientsVerified = true,
                    Status = "published",
                    ViewCount = random.Next(10, 1000),
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30)),
                    IsDeleted = false
                };

                post.PostCategories.Add(new PostCategory { CategoryId = category.Id });
                post.Media.Add(new PostMedia
                {
                    MediaUrl = data.Img,
                    MediaType = "image",
                    IsPrimary = true,
                    DisplayOrder = 1,
                    ProcessingStatus = "ready",
                    CreatedAt = DateTime.UtcNow
                });
                post.PostSteps.Add(new PostStep { StepNumber = 1, Description = "Prepare all ingredients and wash them thoroughly." });
                post.PostSteps.Add(new PostStep { StepNumber = 2, Description = "Cook the ingredients over medium heat." });
                post.PostSteps.Add(new PostStep { StepNumber = 3, Description = "Serve warm and enjoy!" });

                posts.Add(post);
            }
        }

        if (posts.Count > 0)
        {
            await context.Posts.AddRangeAsync(posts);
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"Successfully updated/seeded {recipeData.Length} demo posts.");
    }
}
