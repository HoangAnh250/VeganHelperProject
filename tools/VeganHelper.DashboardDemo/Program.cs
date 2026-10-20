using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;
using VeganHelper.DAL.Repositories;

const string Prefix = "vh_dashboard_demo_";
const string Marker = "Demonstration dataset: dashboard-demo-v1. This is an original sample post, not a submission from a real community member.";
var records = JsonSerializer.Deserialize<List<SeedPost>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "posts.json")),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("Missing demo data.");
if (records.Count != 42 || records.Select(r => r.Title).Distinct().Count() != records.Count ||
    records.Any(r => r.Title.Length < 12 || r.Title.Length > 200 || string.IsNullOrWhiteSpace(r.Category) ||
        r.Diet is not ("vegan" or "lacto_ovo_vegetarian") || (r.Content is null && (r.Steps?.Count != 4 || string.IsNullOrWhiteSpace(r.Ingredients)))))
    throw new InvalidOperationException("Dataset must contain 42 distinct complete English posts.");

if (!args.Any(a => a is "--apply" or "--verify" or "--prepare-local-test"))
{
    Console.WriteLine(JsonSerializer.Serialize(new { mode = "preview", members = 24, publishedPosts = records.Count,
        heldPosts = 3, comments = 14, flags = 8, recipeSteps = records.Sum(r => r.Steps?.Count ?? 0),
        note = "No database connection or writes. Use --apply explicitly to import." }));
    return;
}

var connection = DatabaseConnection.ReadPostgresConnection();
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
if (args.Contains("--prepare-local-test"))
{
    var local = new NpgsqlConnectionStringBuilder(connection);
    if (local.Host != "127.0.0.1" || local.Database != "dashboard_demo_test")
        throw new InvalidOperationException("Local preparation only permits 127.0.0.1/dashboard_demo_test.");
    await db.Database.MigrateAsync();
    Console.WriteLine("Prepared isolated local test database.");
    return;
}
if (args.Contains("--verify"))
{
    Console.WriteLine(JsonSerializer.Serialize(await VerifyAsync()));
    return;
}

await using var transaction = await db.Database.BeginTransactionAsync();
await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtextextended('veganhelper:dashboard-demo:v1', 0))");
var existing = await db.Users.CountAsync(u => EF.Functions.Collate(u.Username, "default").StartsWith(Prefix));
if (existing != 0)
{
    if (existing != 24 || await db.Posts.CountAsync(p => p.Content != null && p.Content.EndsWith(Marker)) != 45)
        throw new InvalidOperationException("An incomplete or conflicting demo batch exists; refusing to overwrite it.");
    await transaction.RollbackAsync();
    Console.WriteLine("Demo batch already exists; no rows added or changed.");
    Console.WriteLine(JsonSerializer.Serialize(await VerifyAsync()));
    return;
}

var now = DateTime.UtcNow;
var day = now.Date;
var memberRole = await db.Roles.SingleAsync(r => r.RoleName == "member");
string[] names = ["Alex Green", "Maya Reed", "Linh Tran", "Noah Brooks", "Sofia Lane", "Minh Nguyen",
    "Olivia Park", "Daniel Le", "Emma Stonefield", "Leo Nguyen", "Grace Chen", "Sam Rivera",
    "Chloe Morgan", "Kai Tran", "Amelia Hart", "Ethan Lam", "Nina Patel", "Lucas Vo",
    "Isla Wong", "Ben Carter", "Hana Pham", "Theo Ellis", "An Taylor", "Ruby Nguyen"];
int[] registrationDaysAgo = [175,160,145,132,120,110,100,90,80,72,65,60,55,49,43,37,29,24,19,14,9,5,2,0];
var members = names.Select((name, i) => new User {
    Username = Prefix + (i + 1).ToString("D2"), Email = $"dashboard-demo-{i + 1:D2}@veganhelper.invalid",
    RoleId = memberRole.Id, IsActive = false, PasswordHash = null, CreatedAt = day.AddDays(-registrationDaysAgo[i]),
    EmailVerifiedAt = null, TokenVersion = 1
}).ToList();
db.Users.AddRange(members);
await db.SaveChangesAsync();
db.UserProfiles.AddRange(members.Select((user, i) => new UserProfile {
    UserId = user.Id, DisplayName = names[i] + " (Demo)", DietType = i % 5 == 0 ? "lacto_ovo_vegetarian" : "vegan"
}));

var categories = await db.Categories.Where(c => c.CategoryType == "post" && c.IsActive).ToListAsync();
var categoryNames = new Dictionary<string, (string Slug, string Kind)> {
    ["Braised Dishes"] = ("braised-dishes", "recipe"), ["Stir-fried Dishes"] = ("stir-fried-dishes", "recipe"),
    ["Soups"] = ("soups", "recipe"), ["Hot Pots"] = ("hot-pots", "recipe"), ["Vegan Pastries"] = ("vegan-pastries", "recipe"),
    ["Vietnamese Vegan Foods"] = ("vietnamese-vegan-foods", "food"), ["Western Vegan Foods"] = ("western-vegan-foods", "food"),
    ["Western Vegetarian Foods"] = ("western-vegetarian-foods", "food")
};
foreach (var name in records.Select(r => r.Category).Distinct())
{
    if (categories.Any(c => c.Name == name)) continue;
    var spec = categoryNames[name];
    if (await db.Categories.AnyAsync(c => c.Slug == spec.Slug)) throw new InvalidOperationException("Conflicting or inactive category slug: " + spec.Slug);
    var category = new Category { Name = name, Slug = spec.Slug, CategoryType = "post", PostCategoryKind = spec.Kind, IsActive = true };
    categories.Add(category); db.Categories.Add(category);
}
await db.SaveChangesAsync();

var posts = new List<Post>();
for (var i = 0; i < records.Count; i++)
{
    var item = records[i];
    var daysAgo = i < 26 ? (i * 7) % 30 : i < 36 ? 31 + (i - 26) * 2 : 65 + (i - 36) * 18;
    var created = day.AddDays(-daysAgo).AddHours(2).AddMinutes(i);
    if (created > now) created = now.AddMinutes(-1);
    var eligible = members.Where(u => u.CreatedAt <= created).ToList();
    var author = eligible[i % eligible.Count];
    var content = item.Content ?? $"A practical home-cooking recipe with measured ingredients and step-by-step instructions. Adjust seasoning to taste.\n\nIngredients\n{item.Ingredients}\n\nMethod\n" +
        string.Join("\n", item.Steps!.Select((step, number) => $"{number + 1}. {step}"));
    var post = new Post { AuthorId = author.Id, Title = item.Title, Content = content + "\n\n" + Marker,
        Status = "published", DietType = item.Diet, ContentRevision = 1, CreatedAt = created, ViewCount = 0,
        PostCategories = [new PostCategory { CategoryId = categories.Single(c => c.Name == item.Category).Id }],
        PostSteps = item.Steps?.Select((step, number) => new PostStep { StepNumber = number + 1, Description = step }).ToList() ?? [] };
    posts.Add(post); db.Posts.Add(post);
}

string[] heldTitles = ["Sweet Lime Dressing with Honey", "Vegetable Broth with Fish Sauce", "Creamy Tomato Soup with Dairy Cream"];
string[] heldBodies = [
    "This demonstration recipe labels a dressing as vegan but lists honey. The ingredient label needs correction or a plant-based substitution before publication.",
    "This demonstration broth is described as vegan but lists fish sauce. A reviewer should request an ingredient correction and accurate dietary labeling.",
    "This demonstration soup is labeled vegan while listing dairy cream. The author needs to choose a plant-based cream or change the dietary description."
];
var held = heldTitles.Select((title, i) => new Post { AuthorId = members[i].Id, Title = title,
    Content = heldBodies[i] + "\n\n" + Marker, DietType = "vegan", Status = "pending_review", ModerationHold = true,
    ContentRevision = 1, CreatedAt = now.AddDays(-3).AddMinutes(i), PostCategories = [new PostCategory { CategoryId = categories.Single(c => c.Name == "Soups").Id }] }).ToList();
db.Posts.AddRange(held);
await db.SaveChangesAsync();

string[] commentTexts = [
    "The separate tofu-browning step makes the texture much clearer. Thank you for explaining when to return it to the pan.",
    "Would shiitake and oyster mushrooms work together here? I like that the broth is kept light before reducing the sauce.",
    "Roasting the pumpkin first is a useful detail. I will keep the seed topping separate so everyone can choose their own.",
    "The tomato broth sounds lovely with fresh herbs. Is a thinner rice noodle your preference for this bowl?",
    "Keeping the stems separate from the leaves is a helpful tip. It is easy to overcook the greens otherwise.",
    "I appreciate the instruction to reduce the sauce with the lid off. It helps explain the texture to look for.",
    "The crunchy vegetables make this bowl sound especially good. I would serve the dressing separately for my family.",
    "Thanks for mentioning extra water if the lentils thicken too much. That is a practical detail for a weeknight recipe.",
    "This looks like a comforting dish to serve with plain rice. The black pepper finish sounds delicious.",
    "Cooking the mushrooms before adding the greens makes sense. Thank you for keeping the steps easy to follow.",
    "A hot pot with the ingredients on separate plates sounds great for a shared dinner. I would add the greens at the end.",
    "I like the balance of pineapple and tamarind in the broth. The reminder to taste before adding more seasoning is useful."
];
var comments = commentTexts.Select((text, i) => new Comment { PostId = posts[i].Id,
    UserId = members.First(u => u.Id != posts[i].AuthorId).Id, Content = text + " [Demo comment]", Status = "visible",
    CreatedAt = posts[i].CreatedAt.AddMinutes(30 + i) < now ? posts[i].CreatedAt.AddMinutes(30 + i) : now }).ToList();
var reportedComments = new[] {
    new Comment { PostId = posts[0].Id, UserId = members[3].Id, Content = "[Demo moderation fixture] Join my unrelated promotional group instead of discussing this recipe.", Status = "visible", CreatedAt = posts[0].CreatedAt > now.AddHours(-2) ? posts[0].CreatedAt : now.AddHours(-2) },
    new Comment { PostId = posts[1].Id, UserId = members[4].Id, Content = "[Demo moderation fixture] Stop sharing recipes here; I am repeating this message across the discussion.", Status = "visible", CreatedAt = now.AddHours(-1) }
};
db.Comments.AddRange(comments); db.Comments.AddRange(reportedComments);
await db.SaveChangesAsync();
db.Flags.AddRange(held.Select((post, i) => new Flag { PostId = post.Id, PostRevision = 1, SourceType = "ai",
    AiModelName = "demo-fixture-not-a-provider-response", Status = "pending",
    Reason = "[Demo AI fixture; not a Gemini result] " + heldBodies[i], CreatedAt = now.AddHours(-3).AddMinutes(i) }));
foreach (var (postIndex, reporterIndex, reason) in new[] {
    (17, 20, "Please check the allergen wording: the title mentions peanuts, while the recipe still contains sesame, soy and wheat."),
    (17, 21, "An ingredient clarification would help readers understand that peanut-free does not mean free from all allergens."),
    (33, 22, "Please review the dipping-sauce ingredient disclosure so readers can distinguish plant-based options from fish-based products.") })
    db.Flags.Add(new Flag { PostId = posts[postIndex].Id, ReporterId = members[reporterIndex].Id, PostRevision = 1,
        SourceType = "user", Status = "pending", Reason = "[Demo report] " + reason, CreatedAt = now.AddHours(-2) });
db.Flags.AddRange(reportedComments.Select((comment, i) => new Flag { CommentId = comment.Id, ReporterId = members[22 + i].Id,
    SourceType = "user", Status = "pending", Reason = "[Demo report] Off-topic promotional or repeated disruptive comment; please review against community guidelines.", CreatedAt = now.AddMinutes(-45 + i) }));
await db.SaveChangesAsync();
var verification = await VerifyAsync();
if (verification.Members != 24 || verification.Posts != 45 || verification.Comments != 14 || verification.Flags != 8 ||
    verification.LoginEnabled != 0 || verification.QueuedScans != 0 || verification.Notifications != 0)
    throw new InvalidOperationException("Post-import verification failed; transaction will roll back.");
await transaction.CommitAsync();
Console.WriteLine("Imported dashboard-demo-v1 successfully in one transaction.");
Console.WriteLine(JsonSerializer.Serialize(verification));

async Task<SeedVerification> VerifyAsync()
{
    var ids = db.Users.Where(u => EF.Functions.Collate(u.Username, "default").StartsWith(Prefix)).Select(u => u.Id);
    var postIds = db.Posts.Where(p => ids.Contains(p.AuthorId)).Select(p => p.Id);
    var commentIds = db.Comments.Where(c => ids.Contains(c.UserId)).Select(c => c.Id);
    var end = DateTime.UtcNow.Date.AddDays(1);
    var dashboard = await new AdminDashboardRepository(db).OverviewAsync(end.AddDays(-30), end, end.AddDays(-60), default);
    return new(await ids.CountAsync(), await postIds.CountAsync(),
        await db.Posts.CountAsync(p => postIds.Contains(p.Id) && p.Status == "published" && !p.ModerationHold),
        await commentIds.CountAsync(), await db.Flags.CountAsync(f => postIds.Contains(f.PostId ?? 0) || commentIds.Contains(f.CommentId ?? 0)),
        await db.Users.CountAsync(u => ids.Contains(u.Id) && (u.IsActive || u.PasswordHash != null)),
        await db.Set<PostModerationScan>().CountAsync(s => postIds.Contains(s.PostId) && (s.State == "queued" || s.State == "processing")),
        await db.Set<Notification>().CountAsync(n => ids.Contains(n.UserId)), dashboard);
}

sealed record SeedPost(string Title, string Category, string Diet, string? Ingredients, List<string>? Steps, string? Content);
sealed record SeedVerification(int Members, int Posts, int PublishedPosts, int Comments, int Flags, int LoginEnabled,
    int QueuedScans, int Notifications, DashboardOverviewProjection Dashboard);
