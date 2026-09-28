namespace VeganHelper.IntegrationTests;

using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Testcontainers.MsSql;
using VeganHelper.DAL.Persistence;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;
using Xunit;

public class PostRepositoryTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer;
    private AppDbContext _context;
    private PostRepository _postRepository;

    public PostRepositoryTests()
    {
        _msSqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_msSqlContainer.GetConnectionString())
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        _postRepository = new PostRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _msSqlContainer.DisposeAsync();
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
            Username = "testuser",
            Email = "test@example.com",
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
}
