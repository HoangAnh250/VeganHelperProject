namespace VeganHelper.DAL.Repositories;

using Microsoft.EntityFrameworkCore.Storage;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VeganHelper.DAL.Persistence;
using VeganHelper.DAL.Entities;

using Microsoft.EntityFrameworkCore;
using System.Linq;

public sealed class PostRepository : IPostRepository
{
    private static readonly Dictionary<string, string[]> AllergyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mushroom"] = ["mushroom", "mushrooms", "nấm"],
        ["nấm"] = ["mushroom", "mushrooms", "nấm"],
        ["tomato"] = ["tomato", "tomatoes", "cà chua"],
        ["cà chua"] = ["tomato", "tomatoes", "cà chua"]
    };

    private readonly AppDbContext _context;
    private IDbContextTransaction? _currentTransaction;

    public PostRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<PostRecommendationProjection> Posts, bool HasMore)> SearchRecommendationsAsync(
        PostRecommendationQuery request, CancellationToken cancellationToken = default)
    {
        var query = PublicRecommendationQuery()
            .Where(post => !request.ExcludedPostIds.Contains(post.Id));

        if (request.MealType is not null)
            query = query.Where(post => post.MealType == request.MealType);
        if (request.DietType == "vegan")
            query = query.Where(post => post.DietType == "vegan");
        if (request.StrictOnlyIngredients)
            query = query.Where(post => post.IngredientsVerified);
        if (request.MaxMinutes.HasValue)
            query = query.Where(post => post.PrepTimeMins.HasValue && post.CookingTimeMins.HasValue &&
                post.PrepTimeMins.Value + post.CookingTimeMins.Value <= request.MaxMinutes.Value);

        foreach (var ingredient in request.RequiredIngredients)
        {
            var pattern = ContainsPattern(ingredient);
            query = query.Where(post => post.PostIngredients.Any(item =>
                EF.Functions.ILike(item.Ingredient!.Name, pattern, "\\")));
        }

        var availableTerms = request.StrictOnlyIngredients
            ? request.AvailableIngredients.Concat(request.RequiredIngredients).Distinct(StringComparer.OrdinalIgnoreCase)
            : request.AvailableIngredients;
        var available = availableTerms.Take(3).Select(ContainsPattern).ToArray();
        if (available.Length > 0)
        {
            var first = available[0];
            var second = available.Length > 1 ? available[1] : null;
            var third = available.Length > 2 ? available[2] : null;
            query = query.Where(post => post.PostIngredients.Any(item =>
                EF.Functions.ILike(item.Ingredient!.Name, first, "\\") ||
                (second != null && EF.Functions.ILike(item.Ingredient!.Name, second, "\\")) ||
                (third != null && EF.Functions.ILike(item.Ingredient!.Name, third, "\\"))));
            if (request.StrictOnlyIngredients)
                query = query.Where(post => !post.PostIngredients.Any(item =>
                    !EF.Functions.ILike(item.Ingredient!.Name, first, "\\") &&
                    (second == null || !EF.Functions.ILike(item.Ingredient!.Name, second, "\\")) &&
                    (third == null || !EF.Functions.ILike(item.Ingredient!.Name, third, "\\"))));
        }

        var allergyIngredientIds = request.AllergyIngredientIds ?? Array.Empty<long>();
        if (allergyIngredientIds.Count > 0 || request.Allergies.Count > 0)
        {
            // A verified flag without structured ingredients cannot establish allergy safety.
            query = query.Where(post => post.IngredientsVerified && post.PostIngredients.Any());

            if (allergyIngredientIds.Count > 0)
                query = query.Where(post => !post.PostIngredients.Any(item =>
                    allergyIngredientIds.Contains(item.IngredientId)));

            foreach (var allergy in GetAllergySearchTerms(request.Allergies)
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var pattern = ContainsPattern(allergy);
                query = query.Where(post =>
                    !EF.Functions.ILike(post.Title, pattern, "\\") &&
                    !post.PostIngredients.Any(item =>
                        EF.Functions.ILike(item.Ingredient!.Name, pattern, "\\")));
            }
        }

        var items = await query.OrderByDescending(post => post.CreatedAt)
            .ThenByDescending(post => post.Id)
            .Take(4)
            .Select(post => new PostRecommendationProjection(
                post.Id,
                post.Title,
                post.Media.OrderBy(media => !media.IsPrimary)
                    .ThenBy(media => media.DisplayOrder)
                    .Select(media => media.ThumbnailUrl ?? (media.MediaType == "image" ? media.MediaUrl : null))
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
        return (items.Take(3).ToArray(), items.Count > 3);
    }

    public async Task<IReadOnlyList<PostRecommendationProjection>> GetPublicRecommendationsAsync(
        IReadOnlyList<long> postIds, CancellationToken cancellationToken = default)
    {
        if (postIds.Count == 0) return [];
        return await PublicRecommendationQuery().Where(post => postIds.Contains(post.Id))
            .Select(post => new PostRecommendationProjection(
                post.Id,
                post.Title,
                post.Media.OrderBy(media => !media.IsPrimary)
                    .ThenBy(media => media.DisplayOrder)
                    .Select(media => media.ThumbnailUrl ?? (media.MediaType == "image" ? media.MediaUrl : null))
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Post> PublicRecommendationQuery() => _context.Posts.AsNoTracking()
        .Where(post => !post.IsDeleted && post.Status == "published" && !post.ModerationHold);

    private static IEnumerable<string> GetAllergySearchTerms(IEnumerable<string> allergies)
    {
        foreach (var allergy in allergies)
        {
            if (string.IsNullOrWhiteSpace(allergy))
                continue;

            var name = allergy.Trim();
            if (AllergyAliases.TryGetValue(name, out var aliases))
            {
                foreach (var alias in aliases)
                    yield return alias;
            }
            else
            {
                yield return name;
            }
        }
    }

    private static string ContainsPattern(string value) =>
        $"%{value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";

    public async Task<Post> CreatePostAsync(Post post, CancellationToken cancellationToken = default)
    {
        var entry = await _context.Posts.AddAsync(post, cancellationToken);
        _context.Set<PostModerationScan>().Add(new PostModerationScan { Post = post, Revision = post.ContentRevision });
        await _context.SaveChangesAsync(cancellationToken);
        if (post.Status == "published" && !post.IsDeleted)
        {
            // Snapshot opt-ins now: later follows/enabling never backfill old posts.
            await _context.Database.ExecuteSqlInterpolatedAsync($"""
                WITH audience AS MATERIALIZED (
                    SELECT f.id, f.follower_id FROM user_follows f JOIN users u ON u.id = f.follower_id
                    WHERE f.following_id = {post.AuthorId} AND f.notify_on_new_post
                        AND f.follower_id <> {post.AuthorId} AND u.is_active
                        AND u.deleted_at IS NULL AND u.email_verified_at IS NOT NULL
                    ORDER BY f.id FOR SHARE OF f
                )
                INSERT INTO post_follower_notifications (post_id, follow_id, user_id, created_at)
                SELECT {post.Id}, audience.id, audience.follower_id, {post.CreatedAt} FROM audience
                ON CONFLICT (post_id, follow_id) DO NOTHING
                """, cancellationToken);
        }
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

    public Task<(long TotalCount, System.Collections.Generic.IEnumerable<PostFeedProjection> Items)> GetFeedAsync(
        int pageIndex,
        int pageSize,
        int? categoryId = null,
        string? difficultyLevel = null,
        string? dietType = null,
        int? prepTimeMax = null,
        CancellationToken cancellationToken = default)
        => GetFeedCoreAsync(pageIndex, pageSize, categoryId, difficultyLevel, dietType, prepTimeMax, false, cancellationToken);

    public Task<(long TotalCount, System.Collections.Generic.IEnumerable<PostFeedProjection> Items)> GetTrendingFeedAsync(
        int pageIndex, int pageSize, int? categoryId, string? difficultyLevel,
        string? dietType, int? prepTimeMax, CancellationToken cancellationToken = default)
        => GetFeedCoreAsync(pageIndex, pageSize, categoryId, difficultyLevel, dietType, prepTimeMax, true, cancellationToken);

    private async Task<(long TotalCount, System.Collections.Generic.IEnumerable<PostFeedProjection> Items)> GetFeedCoreAsync(
        int pageIndex, int pageSize, int? categoryId, string? difficultyLevel,
        string? dietType, int? prepTimeMax, bool trending, CancellationToken cancellationToken)
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

        // A rolling 72-hour window measures recent engagement, not lifetime popularity.
        var trendingSince = DateTime.UtcNow.AddDays(-3);
        var orderedQuery = trending
            ? query.OrderByDescending(p => _context.PostLikes.LongCount(l => l.PostId == p.Id && l.CreatedAt >= trendingSince))
                .ThenByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
            : query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id);

        var projectedQuery = orderedQuery
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PostFeedProjection
            {
                Id = p.Id,
                Title = p.Title,
                ThumbnailUrl = p.Media.Where(m => m.IsPrimary).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => m.ThumbnailUrl ?? m.MediaUrl).FirstOrDefault()
                    ?? p.Media.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => m.ThumbnailUrl ?? m.MediaUrl).FirstOrDefault(),
                MediaUrl = p.Media.Where(m => m.IsPrimary).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => m.MediaUrl).FirstOrDefault()
                    ?? p.Media.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => m.MediaUrl).FirstOrDefault(),
                MediaType = p.Media.Where(m => m.IsPrimary).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => m.MediaType).FirstOrDefault()
                    ?? p.Media.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => m.MediaType).FirstOrDefault(),
                CategoryName = _context.Categories.Where(c => _context.PostCategories.Any(pc => pc.PostId == p.Id && pc.CategoryId == c.Id)).Select(c => c.Name).FirstOrDefault(),
                DietType = p.DietType,
                LikeCount = _context.PostLikes.LongCount(l => l.PostId == p.Id),
                AuthorName = _context.UserProfiles.Where(up => up.UserId == p.AuthorId).Select(up => up.DisplayName).FirstOrDefault() 
                             ?? _context.Users.Where(u => u.Id == p.AuthorId).Select(u => u.Username).FirstOrDefault() ?? "Unknown",
                AvatarUrl = _context.UserProfiles.Where(up => up.UserId == p.AuthorId).Select(up => up.AvatarUrl).FirstOrDefault(),
                ViewCount = p.ViewCount,
                CreatedAt = p.CreatedAt,
                CommentCount = _context.Comments.LongCount(c => c.PostId == p.Id && !c.IsDeleted && c.Status == "visible")
            });

        var items = await projectedQuery.ToListAsync(cancellationToken);

        return (totalCount, items);
    }

    public async Task<(long TotalCount, System.Collections.Generic.IEnumerable<PostFeedProjection> Items)> SearchPostsAsync(
        string keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default,
        int? categoryId = null)
    {
        var query = _context.Posts.AsNoTracking()
            .Where(p => !p.IsDeleted && p.Status == "published" && !p.ModerationHold);
        if (!string.IsNullOrEmpty(keyword))
        {
            var escaped = keyword.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = $"%{escaped}%";
            query = query.Where(p => EF.Functions.ILike(p.Title, pattern, "\\")
                || (p.Content != null && EF.Functions.ILike(p.Content, pattern, "\\")));
        }
        if (categoryId.HasValue)
            query = query.Where(p => p.PostCategories.Any(c => c.CategoryId == categoryId.Value));

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
                ThumbnailUrl = p.Media.Where(m => m.IsPrimary).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => m.ThumbnailUrl ?? m.MediaUrl).FirstOrDefault()
                    ?? p.Media.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => m.ThumbnailUrl ?? m.MediaUrl).FirstOrDefault(),
                MediaUrl = p.Media.Where(m => m.IsPrimary).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => m.MediaUrl).FirstOrDefault()
                    ?? p.Media.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => m.MediaUrl).FirstOrDefault(),
                MediaType = p.Media.Where(m => m.IsPrimary).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => m.MediaType).FirstOrDefault()
                    ?? p.Media.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => m.MediaType).FirstOrDefault(),
                CategoryName = _context.Categories.Where(c => _context.PostCategories.Any(pc => pc.PostId == p.Id && pc.CategoryId == c.Id)).Select(c => c.Name).FirstOrDefault(),
                DietType = p.DietType,
                LikeCount = _context.PostLikes.LongCount(l => l.PostId == p.Id),
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
                CreatedAt = p.CreatedAt,
                CommentCount = _context.Comments.LongCount(c => c.PostId == p.Id && !c.IsDeleted && c.Status == "visible")
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
            .FirstOrDefaultAsync(
                // PostService verifies publication/ownership before returning detail or recording a view.
                p => p.Id == postId && !p.IsDeleted,
                cancellationToken);

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
                .SetProperty(p => p.ContentRevision, p => p.ContentRevision + 1)
                .SetProperty(p => p.DeletedAt, DateTime.UtcNow),
            cancellationToken);

        return rowsAffected > 0;
    }

    public Task<PostPublicationState?> LockPublicationAsync(long id, int revision, CancellationToken ct) =>
        _context.Database.SqlQuery<PostPublicationState>($"""
            SELECT status AS "Status", moderation_hold AS "ModerationHold" FROM posts
            WHERE id = {id} AND content_revision = {revision} AND is_deleted = false FOR UPDATE
            """).SingleOrDefaultAsync(ct);

    public Task<bool> IsEvidenceMediaAsync(string url, CancellationToken ct) => _context.Flags.AnyAsync(f =>
        f.ReportedSnapshotJson != null && EF.Functions.JsonContains(f.ReportedSnapshotJson,
            System.Text.Json.JsonSerializer.Serialize(new { media = new[] { new { mediaUrl = url } } })), ct);

    public Task<long> CountCommentsAsync(long postId, CancellationToken cancellationToken = default)
    {
        return _context.Comments
            .Where(c => c.PostId == postId && !c.IsDeleted && c.Status == "visible")
            .LongCountAsync(cancellationToken);
    }

    public async Task<Dictionary<long, long>> CountCommentsForPostsAsync(long[] postIds, CancellationToken cancellationToken = default)
    {
        if (postIds.Length == 0)
        {
            return new Dictionary<long, long>();
        }

        var counts = await _context.Comments
            .Where(c => postIds.Contains(c.PostId) && !c.IsDeleted && c.Status == "visible")
            .GroupBy(c => c.PostId)
            .Select(g => new { PostId = g.Key, Count = g.LongCount() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.PostId, x => x.Count);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task StagePostUpdateRemovalsAsync(Post post, IReadOnlyCollection<PostMedia> media,
        IReadOnlyCollection<PostCategory> categories, IReadOnlyCollection<PostIngredient> ingredients,
        IReadOnlyCollection<PostStep> steps, CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Post update removals require an active transaction.");
        // A post hidden by Admin stays on hold after every author revision until Admin decides again.
        var lastAdminAction = post.ModerationHold
            ? await _context.Set<PostModerationDecision>().AsNoTracking()
                .Where(decision => decision.PostId == post.Id && decision.ActorType == "admin")
                .OrderByDescending(decision => decision.Id).Select(decision => decision.Action)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        if (lastAdminAction != "remove")
            _context.Set<PostModerationScan>().Add(new PostModerationScan { PostId = post.Id, Revision = post.ContentRevision });
        // Required NoAction relationships must be deleted explicitly before collections are replaced.
        if (media.Count > 0)
        {
            var ids = media.Select(m => m.Id).ToArray();
            var summaries = await _context.PostSummaries
                .Where(s => s.PostId == post.Id)
                .ToListAsync(cancellationToken);
            _context.PostSummaries.RemoveRange(summaries.Where(summary =>
                JsonSerializer.Deserialize<long[]>(summary.SourceMediaIdsJson)?.Any(ids.Contains) == true));
            _context.PostMedia.RemoveRange(media);
        }
        // Preserve joins retained by the Supabase update path; delete only removed entities.
        _context.PostCategories.RemoveRange(categories);
        _context.PostIngredients.RemoveRange(ingredients);
        _context.PostSteps.RemoveRange(steps);

        // EF may promote the next cover before deleting the old one. Release the filtered
        // unique index inside this transaction, then let SaveChanges write the chosen cover.
        await _context.PostMedia.Where(m => m.PostId == post.Id && m.IsPrimary)
            .ExecuteUpdateAsync(update => update.SetProperty(m => m.IsPrimary, false), cancellationToken);
        foreach (var retained in post.Media)
        {
            var entry = _context.Entry(retained);
            if (entry.State is not (EntityState.Added or EntityState.Detached))
                entry.Property(m => m.IsPrimary).OriginalValue = false;
        }
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
