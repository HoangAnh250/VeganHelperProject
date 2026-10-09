namespace VeganHelper.BLL.Services;

using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Posts;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;
using VeganHelper.BLL.Services.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class PostService : IPostService
{
    private readonly IPostRepository _postRepository;
    private readonly IValidator<CreatePostRequest> _validator;
    private readonly IValidator<GetMyPostsRequest> _getMyPostsValidator;
    private readonly IValidator<UpdatePostRequest> _updateValidator;
    private readonly IMediaStorageService _mediaStorageService;
    private readonly ILogger<PostService> _logger;
    private readonly IPostInteractionRepository _postInteractionRepository;

    public PostService(
        IPostRepository postRepository, 
        IValidator<CreatePostRequest> validator, 
        IValidator<GetMyPostsRequest> getMyPostsValidator,
        IValidator<UpdatePostRequest> updateValidator,
        IMediaStorageService mediaStorageService,
        IPostInteractionRepository postInteractionRepository,
        ILogger<PostService>? logger = null)
    {
        _postRepository = postRepository;
        _validator = validator;
        _getMyPostsValidator = getMyPostsValidator;
        _updateValidator = updateValidator;
        _mediaStorageService = mediaStorageService;
        _postInteractionRepository = postInteractionRepository;
        _logger = logger ?? NullLogger<PostService>.Instance;
    }

    public async Task<long> CreatePostAsync(CreatePostRequest request, long authorId, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var post = new Post
        {
            AuthorId = authorId,
            Title = request.Title,
            PostType = request.PostType,
            Content = request.Content,
            DifficultyLevel = string.IsNullOrEmpty(request.DifficultyLevel) ? null : request.DifficultyLevel,
            PrepTimeMins = request.PrepTimeMins,
            CookingTimeMins = request.CookingTimeMins,
            DietType = string.IsNullOrWhiteSpace(request.DietType) ? null : request.DietType,
            Status = "pending_review",
            CreatedAt = DateTime.UtcNow
        };

        // Add Category
        post.PostCategories.Add(new PostCategory
        {
            CategoryId = request.CategoryId
        });

        var ingredients = ParseIngredients(request.IngredientsJson);
        var steps = ParseSteps(request.StepsJson);
        ApplySteps(post, steps);

        // Process Media Files
        var uploadedFileUrls = new List<string>();
        if (request.MediaFiles != null && request.MediaFiles.Count > 0)
        {
            int order = 0;
            foreach (var file in request.MediaFiles)
            {
                var uploadedUrl = await _mediaStorageService.UploadFileAsync(file, "posts");
                uploadedFileUrls.Add(uploadedUrl);
                
                post.Media.Add(new PostMedia
                {
                    MediaUrl = uploadedUrl,
                    MediaType = file.ContentType.StartsWith("video") ? "video" : "image",
                    IsPrimary = order == 0,
                    DisplayOrder = order++,
                    ProcessingStatus = "ready",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        // Save to Database inside a Transaction
        await _postRepository.BeginTransactionAsync(cancellationToken);
        try
        {
            await ApplyIngredientsAsync(post, ingredients, cancellationToken);
            var createdPost = await _postRepository.CreatePostAsync(post, cancellationToken);
            await _postRepository.CommitTransactionAsync(cancellationToken);
            return createdPost.Id;
        }
        catch
        {
            await _postRepository.RollbackTransactionAsync(cancellationToken);
            // Cleanup uploaded files
            foreach (var url in uploadedFileUrls)
            {
                await _mediaStorageService.DeleteFileAsync(url);
            }
            throw;
        }
    }
    public async Task<PagedResult<PostFeedItemDto>> GetFeedAsync(GetFeedRequest request, CancellationToken cancellationToken = default)
    {
        if (request.PageIndex < 1) request.PageIndex = 1;
        if (request.PageSize < 1) request.PageSize = 10;
        if (request.PageSize > 50) request.PageSize = 50;

        var (totalCount, items) = await _postRepository.GetFeedAsync(
            request.PageIndex,
            request.PageSize,
            request.CategoryId,
            request.DifficultyLevel,
            request.DietType,
            request.PrepTimeMax,
            cancellationToken);

        var dtoItems = items.Select(p => new PostFeedItemDto
        {
            Id = p.Id,
            Title = p.Title,
            PostType = p.PostType,
            ThumbnailUrl = p.ThumbnailUrl,
            AuthorName = p.AuthorName,
            AvatarUrl = p.AvatarUrl,
            ViewCount = p.ViewCount,
            CreatedAt = p.CreatedAt
        }).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new PagedResult<PostFeedItemDto>
        {
            TotalItems = totalCount,
            TotalPages = totalPages,
            Items = dtoItems
        };
    }

    public async Task<PagedResult<PostFeedItemDto>> SearchPostsAsync(
        SearchPostsRequest request,
        CancellationToken cancellationToken = default)
    {
        var keyword = request.Keyword?.Trim();
        if (string.IsNullOrWhiteSpace(keyword) || keyword.Length < 2)
        {
            throw new ArgumentException("Keyword must contain at least 2 characters.");
        }

        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize < 1 ? 10 : Math.Min(request.PageSize, 50);

        var (totalCount, items) = await _postRepository.SearchPostsAsync(
            keyword,
            pageIndex,
            pageSize,
            cancellationToken);

        var dtoItems = items.Select(p => new PostFeedItemDto
        {
            Id = p.Id,
            Title = p.Title,
            PostType = p.PostType,
            ThumbnailUrl = p.ThumbnailUrl,
            AuthorName = p.AuthorName,
            AvatarUrl = p.AvatarUrl,
            ViewCount = p.ViewCount,
            CreatedAt = p.CreatedAt
        }).ToList();

        return new PagedResult<PostFeedItemDto>
        {
            TotalItems = totalCount,
            TotalCount = totalCount > int.MaxValue ? int.MaxValue : (int)totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = dtoItems
        };
    }

    public async Task<PostDetailDto> GetPostDetailAsync(
        long postId,
        long? viewerId = null,
        CancellationToken cancellationToken = default)
    {
        var (post, authorName) = await _postRepository.GetPostDetailAsync(postId, cancellationToken);
        
        if (post == null || post.IsDeleted || (post.Status != "published" && post.AuthorId != viewerId))
        {
            throw new VeganHelper.BLL.Exceptions.NotFoundException($"Post with ID {postId} not found.");
        }

        // Increment view count in DB and locally for the return object
        await _postRepository.IncrementViewCountAsync(postId, cancellationToken);
        post.ViewCount++;

        var likeCount = await _postInteractionRepository.CountLikesAsync(postId, cancellationToken);
        var isLiked = false;
        var isSaved = false;
        if (viewerId.HasValue)
        {
            isLiked = await _postInteractionRepository.FindLikeAsync(
                viewerId.Value,
                postId,
                cancellationToken) is not null;
            isSaved = await _postInteractionRepository.FindSavedPostAsync(
                viewerId.Value,
                postId,
                cancellationToken) is not null;
        }

        return new PostDetailDto
        {
            Id = post.Id,
            AuthorId = post.AuthorId,
            AuthorName = authorName,
            PostType = post.PostType,
            Title = post.Title,
            Content = post.Content,
            CategoryId = post.PostCategories.FirstOrDefault()?.CategoryId ?? 0,
            DifficultyLevel = post.DifficultyLevel,
            PrepTimeMins = post.PrepTimeMins,
            CookingTimeMins = post.CookingTimeMins,
            DietType = post.DietType,
            Status = post.Status,
            ViewCount = post.ViewCount,
            LikeCount = likeCount,
            IsLiked = isLiked,
            IsSaved = isSaved,
            CreatedAt = post.CreatedAt,
            Media = post.Media.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => new PostMediaDto
            {
                Id = m.Id,
                DisplayOrder = m.DisplayOrder,
                MediaUrl = m.MediaUrl,
                MediaType = m.MediaType,
                IsPrimary = m.IsPrimary
            }).ToList(),
            Ingredients = post.PostIngredients.Select(pi => new PostIngredientDto
            {
                IngredientId = pi.IngredientId,
                Name = pi.Ingredient?.Name ?? "Unknown", // Assuming Ingredient table is eagerly loaded and has Name
                Quantity = pi.Quantity ?? 0m,
                Unit = pi.Unit
            }).ToList(),
            Steps = post.PostSteps.Select(ps => new PostStepDto
            {
                StepNumber = ps.StepNumber,
                Instruction = ps.Description
            }).OrderBy(s => s.StepNumber).ToList()
        };
    }


    public async Task<PagedResult<MyPostItemDto>> GetMyPostsAsync(long authorId, GetMyPostsRequest request, CancellationToken cancellationToken = default)
    {
        await _getMyPostsValidator.ValidateAndThrowAsync(request, cancellationToken);

        var (posts, totalCount) = await _postRepository.GetMyPostsAsync(
            authorId, 
            request.Status, 
            request.PageIndex, 
            request.PageSize, 
            cancellationToken);

        var items = posts.Select(p => new MyPostItemDto
        {
            Id = p.Id,
            Title = p.Title,
            Status = p.Status,
            CreatedAt = p.CreatedAt,
            ThumbnailUrl = p.Media.Where(m => m.IsPrimary).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => m.MediaUrl).FirstOrDefault(),
            Content = p.Content,
            PostType = p.PostType,
            CategoryName = null,
            CategoryId = p.PostCategories.OrderBy(c => c.CategoryId).Select(c => c.CategoryId).FirstOrDefault(),
            ViewCount = p.ViewCount
        }).ToList();

        int totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new PagedResult<MyPostItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
            TotalPages = totalPages
        };
    }

    public async Task DeletePostAsync(long postId, long authorId, CancellationToken cancellationToken = default)
    {
        // Owner operations must also work for pending/rejected posts. The public
        // detail query intentionally returns only published posts, so use the
        // owner/update query for authorization and soft deletion instead.
        var post = await _postRepository.GetPostForUpdateAsync(postId, cancellationToken);
        if (post == null)
        {
            throw new VeganHelper.BLL.Exceptions.NotFoundException($"Post with ID {postId} not found.");
        }

        if (post.AuthorId != authorId)
        {
            throw new UnauthorizedAccessException("You are not authorized to delete this post.");
        }

        bool deleted = await _postRepository.DeletePostAsync(postId, cancellationToken);
        if (!deleted)
        {
            throw new VeganHelper.BLL.Exceptions.NotFoundException($"Post with ID {postId} could not be deleted.");
        }

        // Clean up media files from cloud storage
        if (post.Media != null && post.Media.Any())
        {
            foreach (var media in post.Media)
            {
                await _mediaStorageService.DeleteFileAsync(media.MediaUrl);
            }
        }
    }

    public async Task UpdatePostAsync(long postId, UpdatePostRequest request, long authorId, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var post = await _postRepository.GetPostForUpdateAsync(postId, cancellationToken);

        if (post == null)
        {
            throw new VeganHelper.BLL.Exceptions.NotFoundException($"Post with ID {postId} not found.");
        }

        if (post.AuthorId != authorId)
        {
            throw new UnauthorizedAccessException("You are not authorized to update this post.");
        }

        var ingredients = ParseIngredients(request.IngredientsJson);
        var steps = ParseSteps(request.StepsJson);
        var removeIds = (request.MediaIdsToRemove ?? []).Distinct().ToHashSet();
        if (removeIds.Any(id => id <= 0 || !post.Media.Any(m => m.Id == id)))
            throw new ArgumentException("Every media ID to remove must belong to this post.");
        var files = request.MediaFilesToAdd ?? [];
        var finalCount = post.Media.Count - removeIds.Count + files.Count;
        if (finalCount < 1) throw new ArgumentException("At least 1 media file is required for thumbnail.");
        if (finalCount > 10) throw new ArgumentException("A post can contain at most 10 media files.");
        foreach (var file in files) await PostMediaUploadValidation.ValidateAsync(file, cancellationToken);

        var removed = post.Media.Where(m => removeIds.Contains(m.Id)).ToList();
        var originalCategories = post.PostCategories.ToArray();
        var originalIngredients = post.PostIngredients.ToArray();
        var originalSteps = post.PostSteps.ToArray();

        // Update basic fields
        post.ContentRevision = checked(post.ContentRevision + 1);
        post.Title = request.Title;
        post.Content = request.Content;
        post.DifficultyLevel = string.IsNullOrEmpty(request.DifficultyLevel) ? null : request.DifficultyLevel;
        post.PrepTimeMins = request.PrepTimeMins;
        post.CookingTimeMins = request.CookingTimeMins;
        post.DietType = string.IsNullOrWhiteSpace(request.DietType) ? null : request.DietType;
        post.Status = "pending_review";
        post.UpdatedAt = DateTime.UtcNow;

        // Update Categories
        foreach (var oldCategory in post.PostCategories.Where(c => c.CategoryId != request.CategoryId).ToList())
            post.PostCategories.Remove(oldCategory);
        if (!post.PostCategories.Any(c => c.CategoryId == request.CategoryId))
            post.PostCategories.Add(new PostCategory { CategoryId = request.CategoryId });

        var uploadedFileUrls = new List<string>();
        var transactionStarted = false;
        try
        {
            // Complete uploads before DB writes, and compensate partial batches on failure.
            var additions = new List<PostMedia>();
            foreach (var file in files)
            {
                var url = await _mediaStorageService.UploadFileAsync(file, "posts");
                uploadedFileUrls.Add(url);
                additions.Add(new PostMedia
                {
                    MediaUrl = url,
                    MediaType = file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? "video" : "image",
                    ProcessingStatus = "ready",
                    CreatedAt = DateTime.UtcNow
                });
            }
            foreach (var media in removed) post.Media.Remove(media);
            var ordered = post.Media.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).ToList();
            var primary = ordered.FirstOrDefault(m => m.IsPrimary);
            if (primary is not null)
            {
                ordered.Remove(primary);
                ordered.Insert(0, primary);
            }
            ordered.AddRange(additions);
            for (var index = 0; index < ordered.Count; index++)
            {
                ordered[index].DisplayOrder = index;
                ordered[index].IsPrimary = index == 0;
            }
            foreach (var media in additions) post.Media.Add(media);

            await _postRepository.BeginTransactionAsync(cancellationToken);
            transactionStarted = true;
            if (!string.IsNullOrWhiteSpace(request.IngredientsJson))
                await ApplyIngredientsAsync(post, ingredients, cancellationToken);
            if (!string.IsNullOrWhiteSpace(request.StepsJson)) ApplySteps(post, steps);
            await _postRepository.StagePostUpdateRemovalsAsync(post, removed,
                originalCategories.Where(c => !post.PostCategories.Contains(c)).ToArray(),
                originalIngredients.Where(i => !post.PostIngredients.Contains(i)).ToArray(),
                originalSteps.Where(s => !post.PostSteps.Contains(s)).ToArray(), cancellationToken);
            await _postRepository.SaveChangesAsync(cancellationToken);
            await _postRepository.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            if (transactionStarted)
            {
                try { await _postRepository.RollbackTransactionAsync(CancellationToken.None); }
                catch (Exception rollbackError) { _logger.LogError(rollbackError, "Failed to roll back post {PostId} update.", postId); }
            }
            await CleanupMediaAsync(uploadedFileUrls, postId);
            throw;
        }

        // Storage cleanup is outside the DB failure handler: never delete committed additions.
        var obsoleteUrls = removed.Select(m => m.MediaUrl).Distinct()
            .Where(url => !post.Media.Any(m => m.MediaUrl == url));
        await CleanupMediaAsync(obsoleteUrls, postId);
    }

    private async Task CleanupMediaAsync(IEnumerable<string> urls, long postId)
    {
        foreach (var url in urls)
        {
            try { await _mediaStorageService.DeleteFileAsync(url); }
            catch (Exception error)
            {
                // Keep the successful DB update intact. This orphan requires later cleanup.
                _logger.LogWarning(error, "Post {PostId} media cleanup failed for {MediaUrl}.", postId, url);
            }
        }
    }

    private static List<T> ParseList<T>(string? json, string field) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var items = JsonSerializer.Deserialize<List<T>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (items is null || items.Any(item => item is null))
                throw new ArgumentException($"{field} must be a JSON array of objects.");
            return items;
        }
        catch (JsonException)
        {
            throw new ArgumentException($"Invalid JSON format for {field}.");
        }
    }

    private static List<IngredientDto> ParseIngredients(string? json)
    {
        var ingredients = ParseList<IngredientDto>(json, "Ingredients");
        foreach (var ingredient in ingredients)
        {
            ingredient.Name = ingredient.Name?.Trim() ?? string.Empty;
            ingredient.Unit = ingredient.Unit?.Trim() ?? string.Empty;
            if (ingredient.IngredientId < 0 || (ingredient.IngredientId == 0 && ingredient.Name.Length == 0))
                throw new ArgumentException("Each ingredient needs a valid IngredientId or a name.");
            if (ingredient.Name.Length > 100 || ingredient.Unit.Length > 20)
                throw new ArgumentException("Ingredient names cannot exceed 100 characters and units cannot exceed 20 characters.");
            if (ingredient.Quantity.HasValue)
            {
                ingredient.Quantity = Math.Round(ingredient.Quantity.Value, 3, MidpointRounding.AwayFromZero);
                if (ingredient.Quantity <= 0 || ingredient.Quantity >= 1000000000m)
                    throw new ArgumentException("Ingredient quantity must be positive at three-decimal precision and less than 1,000,000,000.");
            }
        }
        return ingredients;
    }

    private static List<StepDto> ParseSteps(string? json)
    {
        var steps = ParseList<StepDto>(json, "Steps");
        if (steps.Any(s => s.StepNumber <= 0 || string.IsNullOrWhiteSpace(s.Description))
            || steps.Select(s => s.StepNumber).Distinct().Count() != steps.Count)
            throw new ArgumentException("Cooking steps require unique positive step numbers and a description.");
        return steps;
    }

    private async Task ApplyIngredientsAsync(Post post, List<IngredientDto> ingredients, CancellationToken cancellationToken)
    {
        var resolved = new List<PostIngredient>();
        var ids = new HashSet<long>();
        foreach (var item in ingredients)
        {
            var ingredient = item.IngredientId > 0
                ? await _postRepository.FindIngredientByIdAsync(item.IngredientId, cancellationToken)
                : await _postRepository.GetOrCreateIngredientAsync(item.Name, item.Unit, cancellationToken);
            if (ingredient is null) throw new ArgumentException("An ingredient ID does not exist.");
            if (!ids.Add(ingredient.Id)) throw new ArgumentException("The same ingredient cannot appear more than once in a recipe.");
            resolved.Add(new PostIngredient { IngredientId = ingredient.Id, Quantity = item.Quantity, Unit = item.Unit });
        }

        // Retain tracked joins with the same composite key when editing a recipe.
        foreach (var old in post.PostIngredients.Where(i => !ids.Contains(i.IngredientId)).ToList())
            post.PostIngredients.Remove(old);
        foreach (var item in resolved)
        {
            var existing = post.PostIngredients.FirstOrDefault(i => i.IngredientId == item.IngredientId);
            if (existing is null) post.PostIngredients.Add(item);
            else { existing.Quantity = item.Quantity; existing.Unit = item.Unit; }
        }
    }

    private static void ApplySteps(Post post, List<StepDto> steps)
    {
        var numbers = steps.Select(s => s.StepNumber).ToHashSet();
        foreach (var old in post.PostSteps.Where(s => !numbers.Contains(s.StepNumber)).ToList()) post.PostSteps.Remove(old);
        foreach (var step in steps)
        {
            var existing = post.PostSteps.FirstOrDefault(s => s.StepNumber == step.StepNumber);
            if (existing is null) post.PostSteps.Add(new PostStep { StepNumber = step.StepNumber, Description = step.Description.Trim() });
            else existing.Description = step.Description.Trim();
        }
    }
}
