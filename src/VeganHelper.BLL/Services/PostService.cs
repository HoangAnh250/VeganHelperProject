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

public sealed class PostService : IPostService
{
    private readonly IPostRepository _postRepository;
    private readonly IValidator<CreatePostRequest> _validator;
    private readonly IValidator<GetMyPostsRequest> _getMyPostsValidator;
    private readonly IValidator<UpdatePostRequest> _updateValidator;
    private readonly IMediaStorageService _mediaStorageService;

    public PostService(
        IPostRepository postRepository, 
        IValidator<CreatePostRequest> validator, 
        IValidator<GetMyPostsRequest> getMyPostsValidator,
        IValidator<UpdatePostRequest> updateValidator,
        IMediaStorageService mediaStorageService)
    {
        _postRepository = postRepository;
        _validator = validator;
        _getMyPostsValidator = getMyPostsValidator;
        _updateValidator = updateValidator;
        _mediaStorageService = mediaStorageService;
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
            Media = post.Media.Select(m => new PostMediaDto
            {
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
            ThumbnailUrl = p.Media.FirstOrDefault(m => m.IsPrimary)?.MediaUrl
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

        // Update Media (simulate)
        var filesToDeleteFromCloud = new List<string>();
        if (request.MediaIdsToRemove != null && request.MediaIdsToRemove.Count > 0)
        {
            var mediaToRemove = post.Media.Where(m => request.MediaIdsToRemove.Contains(m.Id)).ToList();
            foreach (var m in mediaToRemove)
            {
                filesToDeleteFromCloud.Add(m.MediaUrl);
                post.Media.Remove(m);
            }
        }

        var uploadedFileUrls = new List<string>();
        if (request.MediaFilesToAdd != null && request.MediaFilesToAdd.Count > 0)
        {
            int newOrder = post.Media.Count > 0 ? post.Media.Max(m => m.DisplayOrder) + 1 : 0;
            foreach (var file in request.MediaFilesToAdd)
            {
                var uploadedUrl = await _mediaStorageService.UploadFileAsync(file, "posts");
                uploadedFileUrls.Add(uploadedUrl);
                
                post.Media.Add(new PostMedia
                {
                    MediaUrl = uploadedUrl,
                    MediaType = file.ContentType.StartsWith("video") ? "video" : "image",
                    IsPrimary = post.Media.Count == 0 && newOrder == 0,
                    DisplayOrder = newOrder++,
                    ProcessingStatus = "ready",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        // Check if there is still at least 1 media for thumbnail (if required)
        if (post.Media.Count == 0)
        {
            foreach (var url in uploadedFileUrls)
            {
                await _mediaStorageService.DeleteFileAsync(url);
            }
            throw new ArgumentException("At least 1 media file is required for thumbnail.");
        }

        // Ensure exactly one primary media exists
        if (post.Media.Count > 0 && !post.Media.Any(m => m.IsPrimary))
        {
            post.Media.OrderBy(m => m.DisplayOrder).First().IsPrimary = true;
        }

        await _postRepository.BeginTransactionAsync(cancellationToken);
        try
        {
            await _postRepository.SaveChangesAsync(cancellationToken);
            await _postRepository.CommitTransactionAsync(cancellationToken);
            
            // Clean up removed files from cloud after successful commit
            foreach (var url in filesToDeleteFromCloud)
            {
                await _mediaStorageService.DeleteFileAsync(url);
            }
        }
        catch
        {
            await _postRepository.RollbackTransactionAsync(cancellationToken);
            // Clean up newly uploaded files since transaction failed
            foreach (var url in uploadedFileUrls)
            {
                await _mediaStorageService.DeleteFileAsync(url);
            }
            throw;
        }
    }

}
