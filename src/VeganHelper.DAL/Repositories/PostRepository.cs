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

    public async Task<(long TotalCount, System.Collections.Generic.IEnumerable<PostFeedProjection> Items)> GetFeedAsync(
        int pageIndex,
        int pageSize,
        int? categoryId = null,
        string? difficultyLevel = null,
        string? dietType = null,
        int? prepTimeMax = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Posts.AsQueryable();

        query = query.Where(p => !p.IsDeleted);

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.PostCategories.Any(pc => pc.CategoryId == categoryId.Value));
        }

        if (!string.IsNullOrEmpty(difficultyLevel))
        {
            query = query.Where(p => p.DifficultyLevel == difficultyLevel);
        }

        if (!string.IsNullOrEmpty(dietType))
        {
            query = query.Where(p => p.DietType == dietType);
        }

        if (prepTimeMax.HasValue)
        {
            query = query.Where(p => p.PrepTimeMins <= prepTimeMax.Value);
        }

        long totalCount = await query.LongCountAsync(cancellationToken);

        var projectedQuery = query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PostFeedProjection
            {
                Id = p.Id,
                Title = p.Title,
                PostType = p.PostType,
                ThumbnailUrl = p.Media.Where(m => m.IsPrimary).Select(m => m.MediaUrl).FirstOrDefault(),
                AuthorName = _context.UserProfiles.Where(up => up.UserId == p.AuthorId).Select(up => up.DisplayName).FirstOrDefault() 
                             ?? _context.Users.Where(u => u.Id == p.AuthorId).Select(u => u.Username).FirstOrDefault() ?? "Unknown",
                AvatarUrl = _context.UserProfiles.Where(up => up.UserId == p.AuthorId).Select(up => up.AvatarUrl).FirstOrDefault(),
                ViewCount = p.ViewCount,
                CreatedAt = p.CreatedAt
            });

        var items = await projectedQuery.ToListAsync(cancellationToken);

        return (totalCount, items);
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
