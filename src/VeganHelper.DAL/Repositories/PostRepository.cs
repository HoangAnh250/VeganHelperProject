namespace VeganHelper.DAL.Repositories;

using Microsoft.EntityFrameworkCore.Storage;
using System.Threading;
using System.Threading.Tasks;
using VeganHelper.DAL.Persistence;
using VeganHelper.DAL.Entities;

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

    public Task<Ingredient?> FindIngredientByIdAsync(long ingredientId, CancellationToken cancellationToken = default)
        => _context.Ingredients.FirstOrDefaultAsync(i => i.Id == ingredientId, cancellationToken);

    public async Task<Ingredient> GetOrCreateIngredientAsync(string name, string unit, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.ToLowerInvariant();
        var existing = await _context.Ingredients.FirstOrDefaultAsync(i => i.Name.ToLower() == normalizedName, cancellationToken);
        if (existing is not null) return existing;

        // Concurrent requests for the same name share the existing unique ingredient.
        // This runs in the post transaction, so a failed recipe does not leave orphan ingredients.
        var inserted = await _context.Ingredients.FromSqlInterpolated($"""
            INSERT INTO ingredients (name, default_unit)
            VALUES ({name}, {unit})
            ON CONFLICT (name) DO UPDATE SET name = EXCLUDED.name
            RETURNING *
            """).ToListAsync(cancellationToken);
        return inserted.Single();
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

        // The feed is public, so only approved/published and non-deleted posts are visible.
        query = query.Where(p => !p.IsDeleted && p.Status == "published");

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.PostCategories.Any(pc => pc.CategoryId == categoryId.Value));
        }

        if (!string.IsNullOrEmpty(difficultyLevel))
        {
            var normalizedDifficulty = difficultyLevel.ToLowerInvariant();
            query = query.Where(p => p.DifficultyLevel != null && p.DifficultyLevel.ToLower() == normalizedDifficulty);
        }

        if (!string.IsNullOrEmpty(dietType))
        {
            var normalizedDiet = dietType.ToLowerInvariant();
            query = query.Where(p => p.DietType != null && p.DietType.ToLower() == normalizedDiet);
        }

        if (prepTimeMax.HasValue)
        {
            query = query.Where(p => p.PrepTimeMins <= prepTimeMax.Value);
        }

        long totalCount = await query.LongCountAsync(cancellationToken);

        var projectedQuery = query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PostFeedProjection
            {
                Id = p.Id,
                Title = p.Title,
                PostType = p.PostType,
                ThumbnailUrl = p.Media.Where(m => m.IsPrimary).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => m.MediaUrl).FirstOrDefault(),
                AuthorName = _context.UserProfiles.Where(up => up.UserId == p.AuthorId).Select(up => up.DisplayName).FirstOrDefault() 
                             ?? _context.Users.Where(u => u.Id == p.AuthorId).Select(u => u.Username).FirstOrDefault() ?? "Unknown",
                AvatarUrl = _context.UserProfiles.Where(up => up.UserId == p.AuthorId).Select(up => up.AvatarUrl).FirstOrDefault(),
                ViewCount = p.ViewCount,
                CreatedAt = p.CreatedAt
            });

        var items = await projectedQuery.ToListAsync(cancellationToken);

        return (totalCount, items);
    }

    public async Task<(long TotalCount, System.Collections.Generic.IEnumerable<PostFeedProjection> Items)> SearchPostsAsync(
        string keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var pattern = $"%{keyword}%";
        var query = _context.Posts
            .AsNoTracking()
            .Where(p => !p.IsDeleted
                && p.Status == "published"
                && (p.PostType == "article" || p.PostType == "video")
                && (EF.Functions.ILike(p.Title, pattern)
                    || (p.Content != null && EF.Functions.ILike(p.Content, pattern))));

        var totalCount = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PostFeedProjection
            {
                Id = p.Id,
                Title = p.Title,
                PostType = p.PostType,
                ThumbnailUrl = p.Media.Where(m => m.IsPrimary).Select(m => m.MediaUrl).FirstOrDefault(),
                AuthorName = _context.UserProfiles
                    .Where(up => up.UserId == p.AuthorId)
                    .Select(up => up.DisplayName)
                    .FirstOrDefault()
                    ?? _context.Users
                        .Where(u => u.Id == p.AuthorId)
                        .Select(u => u.Username)
                        .FirstOrDefault()
                    ?? "Unknown",
                AvatarUrl = _context.UserProfiles
                    .Where(up => up.UserId == p.AuthorId)
                    .Select(up => up.AvatarUrl)
                    .FirstOrDefault(),
                ViewCount = p.ViewCount,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return (totalCount, items);
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
            var normalizedStatus = status.ToLowerInvariant();
            query = query.Where(p => p.Status.ToLower() == normalizedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var posts = await query
            .Include(p => p.Media)
            .Include(p => p.PostCategories)
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (posts, totalCount);
    }

    public async Task<Post?> GetPostForUpdateAsync(long postId, CancellationToken cancellationToken = default)
    {
        return await _context.Posts
            .Include(p => p.Media)
            .Include(p => p.PostCategories)
            .Include(p => p.PostIngredients)
            .Include(p => p.PostSteps)
            .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted, cancellationToken);

    }

    public async Task IncrementViewCountAsync(long postId, CancellationToken cancellationToken = default)
    {
        await _context.Posts
            .Where(p => p.Id == postId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ViewCount, p => p.ViewCount + 1), cancellationToken);
    }

    public async Task<bool> DeletePostAsync(long postId, CancellationToken cancellationToken = default)
    {
        int rowsAffected = await _context.Posts
            .Where(p => p.Id == postId && !p.IsDeleted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.IsDeleted, true)
                .SetProperty(p => p.DeletedAt, DateTime.UtcNow),
            cancellationToken);

        return rowsAffected > 0;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task StagePostUpdateRemovalsAsync(Post post, IReadOnlyCollection<PostMedia> media,
        IReadOnlyCollection<PostCategory> categories, IReadOnlyCollection<PostIngredient> ingredients,
        IReadOnlyCollection<PostStep> steps, CancellationToken cancellationToken = default)
    {
        // Required NoAction relationships must be deleted explicitly before collections are replaced.
        if (media.Count > 0)
        {
            var ids = media.Select(m => m.Id).ToArray();
            var summaries = await _context.PostSummaries
                .Where(s => s.PostId == post.Id && ids.Contains(s.SourceMediaId))
                .ToListAsync(cancellationToken);
            _context.PostSummaries.RemoveRange(summaries);
            _context.PostMedia.RemoveRange(media);
        }
        // Preserve joins retained by the Supabase update path; delete only removed entities.
        _context.PostCategories.RemoveRange(categories);
        _context.PostIngredients.RemoveRange(ingredients);
        _context.PostSteps.RemoveRange(steps);
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
