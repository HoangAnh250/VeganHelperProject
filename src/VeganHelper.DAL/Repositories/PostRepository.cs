namespace VeganHelper.DAL.Repositories;

using Microsoft.EntityFrameworkCore.Storage;
using System.Threading;
using System.Threading.Tasks;
using VeganHelper.DAL.Data;
using VeganHelper.DAL.Models;

using Microsoft.EntityFrameworkCore;
using System.Linq;

public sealed class PostRepository : IPostRepository
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _currentTransaction;

    public PostRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Post> CreatePostAsync(Post post, CancellationToken cancellationToken = default)
    {
        var entry = await _context.Posts.AddAsync(post, cancellationToken);
        return entry.Entity;
    }

    public async Task<(Post? Post, string AuthorName)> GetPostDetailAsync(long postId, CancellationToken cancellationToken = default)
    {
        var post = await _context.Posts
            .Include(p => p.Media)
            .Include(p => p.PostCategories)
            .Include(p => p.PostIngredients)
                .ThenInclude(pi => pi.Ingredient)
            .Include(p => p.PostSteps)
            .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, cancellationToken);

        if (post == null)
        {
            return (null, string.Empty);
        }

        var authorName = await _context.UserProfiles
            .Where(up => up.UserId == post.AuthorId)
            .Select(up => up.DisplayName)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await _context.Users
            .Where(u => u.Id == post.AuthorId)
            .Select(u => u.Username)
            .FirstOrDefaultAsync(cancellationToken)
            ?? "Unknown";

        return (post, authorName);
    }

    public async Task<(List<Post> Posts, int TotalCount)> GetMyPostsAsync(long authorId, string? status, int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Posts
            .Where(p => p.AuthorId == authorId && !p.IsDeleted);

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(p => p.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var posts = await query
            .Include(p => p.Media)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (posts, totalCount);
    }

    public async Task IncrementViewCountAsync(long postId, CancellationToken cancellationToken = default)
    {
        await _context.Posts
            .Where(p => p.Id == postId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ViewCount, p => p.ViewCount + 1), cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            return;
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
            if (_currentTransaction != null)
            {
                await _currentTransaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.RollbackAsync(cancellationToken);
            }
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }
}
