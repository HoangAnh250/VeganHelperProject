namespace VeganHelper.IntegrationTests;

using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Testcontainers.PostgreSql;
using VeganHelper.DAL.Persistence;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;
using Xunit;
using Microsoft.EntityFrameworkCore.Storage;

public partial class PostRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer? _postgresContainer;
    private AppDbContext _context = null!;
    private PostRepository _postRepository = null!;
    private IDbContextTransaction _transaction = null!;

    public PostRepositoryTests()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VEGANHELPER_TEST_POSTGRES")))
            _postgresContainer = new PostgreSqlBuilder("postgres:17").Build();
    }

    public async Task InitializeAsync()
    {
        if (_postgresContainer is not null) await _postgresContainer.StartAsync();

        var connection = _postgresContainer?.GetConnectionString()
            ?? DatabaseConnection.Validate(Environment.GetEnvironmentVariable("VEGANHELPER_TEST_POSTGRES"));

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection)
            .Options;

        _context = new AppDbContext(options);
        if (_postgresContainer is not null) await _context.Database.MigrateAsync();
        else Assert.Empty(await _context.Database.GetPendingMigrationsAsync());

        // All fixture writes roll back, including when running against a shared test database.
        _transaction = await _context.Database.BeginTransactionAsync();

        _postRepository = new PostRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
        await _context.DisposeAsync();
        if (_postgresContainer is not null) await _postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task CreatePostAsync_WhenCalled_SavesToDatabase()
    {
        // Arrange
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "member");
        if (role == null)
        {
            role = new Role { RoleName = "member" };
            _context.Roles.Add(role);
            await _context.SaveChangesAsync();
        }

        var user = new User
        {
            Username = "testuser_" + Guid.NewGuid().ToString("N"),
            Email = Guid.NewGuid().ToString("N") + "@example.com",
            PasswordHash = "hash",
            IsActive = true,
            RoleId = role.Id,
            CreatedAt = System.DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var post = new Post
        {
            AuthorId = user.Id,
            Title = "Integration Test Post",
            PostType = "article",
            Content = "Testing database insertion",
            Status = "pending_review",
            CreatedAt = System.DateTime.UtcNow
        };

        // Act
        var createdPost = await _postRepository.CreatePostAsync(post);
        await _postRepository.SaveChangesAsync();

        // Assert
        var savedPost = await _context.Posts.FirstOrDefaultAsync(p => p.Id == createdPost.Id);
        Assert.NotNull(savedPost);
        Assert.Equal("Integration Test Post", savedPost.Title);
        Assert.Equal("article", savedPost.PostType);
    }

    [Fact]
    public async Task Feed_FiltersAndMapsAuthorMedia_OnPostgreSql()
    {
        var user = await AddUser();
        _context.UserProfiles.Add(new UserProfile { UserId = user.Id, DisplayName = "Bếp Chay", DietType = "vegan" });
        var category = new Category { Name = "Test " + Guid.NewGuid().ToString("N"), Slug = Guid.NewGuid().ToString("N"), CategoryType = "post", PostCategoryKind = "recipe", IsActive = true };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        var post = NewPost(user.Id);
        post.DifficultyLevel = "Easy";
        post.DietType = "vegan";
        post.PrepTimeMins = 10;
        post.PostCategories.Add(new PostCategory { CategoryId = category.Id });
        post.Media.Add(new PostMedia { MediaUrl = "https://example.com/tofu.jpg", MediaType = "image", IsPrimary = true, DisplayOrder = 1, ProcessingStatus = "ready", CreatedAt = DateTime.UtcNow });
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        var result = await _postRepository.GetFeedAsync(1, 10, category.Id, "EASY", "VEGAN", 15);
        var item = Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(post.Id, item.Id);
        Assert.Equal("Bếp Chay", item.AuthorName);
        Assert.Equal("https://example.com/tofu.jpg", item.ThumbnailUrl);
        Assert.Equal(DateTimeKind.Utc, item.CreatedAt.Kind);
        Assert.Empty((await _postRepository.GetFeedAsync(1, 10, category.Id, "Hard")).Items);
    }

    [Fact]
    public async Task MyPosts_LoadsCategoryAndPaginatesEqualTimestampsDeterministically()
    {
        var user = await AddUser();
        var category = await _context.Categories.FirstAsync(c => c.CategoryType == "post");
        var first = NewPost(user.Id);
        var second = NewPost(user.Id);
        second.CreatedAt = first.CreatedAt;
        first.PostCategories.Add(new PostCategory { CategoryId = category.Id });
        second.PostCategories.Add(new PostCategory { CategoryId = category.Id });
        _context.Posts.AddRange(first, second);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var pageOne = await _postRepository.GetMyPostsAsync(user.Id, "PUBLISHED", 1, 1);
        var pageTwo = await _postRepository.GetMyPostsAsync(user.Id, "published", 2, 1);
        Assert.Equal(2, pageOne.TotalCount);
        Assert.Equal(second.Id, Assert.Single(pageOne.Posts).Id);
        Assert.Equal(first.Id, Assert.Single(pageTwo.Posts).Id);
        Assert.Equal(category.Id, Assert.Single(pageOne.Posts[0].PostCategories).CategoryId);
    }

    [Fact]
    public async Task UpdateAndSoftDelete_ExecuteOnPostgreSqlWithCheckConstraints()
    {
        var user = await AddUser();
        var post = NewPost(user.Id);
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();
        await _postRepository.IncrementViewCountAsync(post.Id);
        _context.ChangeTracker.Clear();
        Assert.Equal(1, (await _context.Posts.SingleAsync(p => p.Id == post.Id)).ViewCount);
        Assert.True(await _postRepository.DeletePostAsync(post.Id));
        Assert.False(await _postRepository.DeletePostAsync(post.Id));
        _context.ChangeTracker.Clear();
        Assert.Null((await _postRepository.GetPostDetailAsync(post.Id)).Post);
        Assert.Empty((await _postRepository.GetMyPostsAsync(user.Id, null, 1, 10)).Posts);
    }

    [Fact]
    public async Task AuthLookupAndUniqueness_PreserveCaseInsensitiveIdentity()
    {
        var user = await AddUser();
        var repository = new AuthRepository(_context);
        Assert.Equal(user.Id, (await repository.FindUserByEmailAsync(user.Email.ToUpperInvariant(), default))!.Id);
        Assert.Equal(user.Id, (await repository.FindUserByUsernameAsync(user.Username.ToUpperInvariant(), default))!.Id);
        _context.Users.Add(new User { Username = user.Username.ToUpperInvariant(), Email = Guid.NewGuid().ToString("N") + "@example.com", RoleId = 1, IsActive = true, CreatedAt = DateTime.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    [Fact]
    public async Task Categories_OnlyReturnsActivePostCategories()
    {
        var categories = await new CategoryRepository(_context).GetPostCategoriesAsync();
        Assert.NotEmpty(categories);
        Assert.All(categories, c => { Assert.True(c.IsActive); Assert.Equal("post", c.CategoryType.ToLowerInvariant()); });
        Assert.Equal(categories.OrderBy(c => c.Id).Select(c => c.Id), categories.Select(c => c.Id));
    }

    [Fact]
    public async Task ShopSeed_LoadsUnicodeAndResetsIdentity_InAnIsolatedTemporaryTable()
    {
        // A session-local table shadows public.shops; no imported shop is deleted or changed.
        await _context.Database.ExecuteSqlRawAsync("CREATE TEMP TABLE shops (LIKE public.shops INCLUDING ALL) ON COMMIT DROP");
        await DataSeeder.SeedDataAsync(_context);
        Assert.Equal(100, await _context.Shops.CountAsync());
        Assert.Equal("Lẩu chay Nhà Đan", (await _context.Shops.SingleAsync(s => s.Id == 100)).Name);
        var shop = new Shop { Name = "Temporary identity probe", IsApproved = true, CreatedAt = DateTime.UtcNow };
        _context.Shops.Add(shop);
        await _context.SaveChangesAsync();
        Assert.Equal(101, shop.Id);
    }

    [Fact]
    public async Task Seeds_AreRepeatableWithoutOverwritingExistingPosts()
    {
        // Seed operations run under an advisory lock in the CLI; these writes all roll back.
        await DataSeeder.SeedDataAsync(_context);
        var categoryCount = await _context.Categories.CountAsync();
        var shopCount = await _context.Shops.CountAsync();
        await PostSeeder.SeedPostsAsync(_context);
        var postCount = await _context.Posts.CountAsync();
        var demo = await _context.Posts.FirstAsync(p => p.Title.StartsWith("[Demo]"));
        demo.Content = "User-edited content must be preserved.";
        await _context.SaveChangesAsync();
        await DataSeeder.SeedDataAsync(_context);
        await PostSeeder.SeedPostsAsync(_context);
        Assert.Equal(categoryCount, await _context.Categories.CountAsync());
        Assert.Equal(shopCount, await _context.Shops.CountAsync());
        Assert.Equal(postCount, await _context.Posts.CountAsync());
        Assert.Equal("User-edited content must be preserved.", (await _context.Posts.SingleAsync(p => p.Id == demo.Id)).Content);
        var author = await _context.Users.SingleAsync(u => u.Id == demo.AuthorId);
        Assert.Equal("member", await _context.Roles.Where(r => r.Id == author.RoleId).Select(r => r.RoleName).SingleAsync());
    }

    private async Task<User> AddUser()
    {
        var user = new User { Username = "PgTest_" + Guid.NewGuid().ToString("N"), Email = Guid.NewGuid().ToString("N") + "@example.com", RoleId = 1, IsActive = true, CreatedAt = DateTime.UtcNow };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private static Post NewPost(long authorId) => new()
    {
        AuthorId = authorId, Title = "PostgreSQL test", PostType = "article", Status = "published", CreatedAt = DateTime.UtcNow
    };
}
