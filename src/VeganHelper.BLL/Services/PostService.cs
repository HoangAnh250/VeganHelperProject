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

    public PostService(
        IPostRepository postRepository, 
        IValidator<CreatePostRequest> validator, 
        IValidator<GetMyPostsRequest> getMyPostsValidator,
        IValidator<UpdatePostRequest> updateValidator,
        IMediaStorageService mediaStorageService,
        ILogger<PostService>? logger = null)
    {
        _postRepository = postRepository;
        _validator = validator;
        _getMyPostsValidator = getMyPostsValidator;
        _updateValidator = updateValidator;
        _mediaStorageService = mediaStorageService;
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

        // Parse Ingredients if present
        if (!string.IsNullOrWhiteSpace(request.IngredientsJson))
        {
            try
            {
                var ingredients = JsonSerializer.Deserialize<List<IngredientDto>>(request.IngredientsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (ingredients != null)
                {
                    foreach (var ing in ingredients)
                    {
                        post.PostIngredients.Add(new PostIngredient
                        {
                            IngredientId = ing.IngredientId,
                            Quantity = ing.Quantity,
                            Unit = ing.Unit ?? string.Empty
                        });
                    }
                }
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("Invalid JSON format for Ingredients.", ex);
            }
        }

        // Parse Steps if present
        if (!string.IsNullOrWhiteSpace(request.StepsJson))
        {
            try
            {
                var steps = JsonSerializer.Deserialize<List<StepDto>>(request.StepsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (steps != null)
                {
                    foreach (var step in steps)
                    {
                        post.PostSteps.Add(new PostStep
                        {
                            StepNumber = step.StepNumber,
                            Description = step.Description ?? string.Empty
                        });
                    }
                }
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("Invalid JSON format for Steps.", ex);
            }
        }

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
    public async Task<PostDetailDto> GetPostDetailAsync(long postId, CancellationToken cancellationToken = default)
    {
        var (post, authorName) = await _postRepository.GetPostDetailAsync(postId, cancellationToken);
        
        if (post == null)
        {
            throw new VeganHelper.BLL.Exceptions.NotFoundException($"Post with ID {postId} not found.");
        }

        // Increment view count in DB and locally for the return object
        await _postRepository.IncrementViewCountAsync(postId, cancellationToken);
        post.ViewCount++;

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
            ThumbnailUrl = p.Media.FirstOrDefault(m => m.IsPrimary)?.MediaUrl,
            Content = p.Content,
            PostType = p.PostType,
            CategoryName = null,
            CategoryId = p.PostCategories.FirstOrDefault() != null ? p.PostCategories.FirstOrDefault().CategoryId : 0,
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
        var (post, _) = await _postRepository.GetPostDetailAsync(postId, cancellationToken);
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

        var removeIds = (request.MediaIdsToRemove ?? []).Distinct().ToHashSet();
        if (removeIds.Any(id => id <= 0 || !post.Media.Any(m => m.Id == id)))
            throw new ArgumentException("Every media ID to remove must belong to this post.");
        var files = request.MediaFilesToAdd ?? [];
        var finalCount = post.Media.Count - removeIds.Count + files.Count;
        if (finalCount < 1) throw new ArgumentException("At least 1 media file is required for thumbnail.");
        if (finalCount > 10) throw new ArgumentException("A post can contain at most 10 media files.");
        foreach (var file in files) await PostMediaUploadValidation.ValidateAsync(file, cancellationToken);

        var removed = post.Media.Where(m => removeIds.Contains(m.Id)).ToList();
        await _postRepository.StagePostUpdateRemovalsAsync(post, removed,
            !string.IsNullOrWhiteSpace(request.IngredientsJson), cancellationToken);

        // Update basic fields
        post.Title = request.Title;
        post.Content = request.Content;
        post.DifficultyLevel = string.IsNullOrEmpty(request.DifficultyLevel) ? null : request.DifficultyLevel;
        post.PrepTimeMins = request.PrepTimeMins;
        post.CookingTimeMins = request.CookingTimeMins;
        post.DietType = string.IsNullOrWhiteSpace(request.DietType) ? null : request.DietType;
        post.Status = "pending_review";
        post.UpdatedAt = DateTime.UtcNow;

        // Update Categories
        post.PostCategories.Clear();
        post.PostCategories.Add(new PostCategory { CategoryId = request.CategoryId });

        // Update Ingredients
        if (!string.IsNullOrWhiteSpace(request.IngredientsJson))
        {
            try
            {
                var ingredients = JsonSerializer.Deserialize<List<IngredientDto>>(request.IngredientsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                post.PostIngredients.Clear();
                if (ingredients != null)
                {
                    foreach (var ing in ingredients)
                    {
                        post.PostIngredients.Add(new PostIngredient
                        {
                            IngredientId = ing.IngredientId,
                            Quantity = ing.Quantity,
                            Unit = ing.Unit ?? string.Empty
                        });
                    }
                }
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("Invalid JSON format for Ingredients.", ex);
            }
        }

        // Update Steps
        if (!string.IsNullOrWhiteSpace(request.StepsJson))
        {
            try
            {
                var steps = JsonSerializer.Deserialize<List<StepDto>>(request.StepsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                post.PostSteps.Clear();
                if (steps != null)
                {
                    foreach (var step in steps)
                    {
                        post.PostSteps.Add(new PostStep
                        {
                            StepNumber = step.StepNumber,
                            Description = step.Description ?? string.Empty
                        });
                    }
                }
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("Invalid JSON format for Steps.", ex);
            }
        }

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

}
