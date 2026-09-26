namespace VeganHelper.BLL.Services;

using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Posts;
using VeganHelper.DAL.Models;
using VeganHelper.DAL.Repositories;

public sealed class PostService : IPostService
{
    private readonly IPostRepository _postRepository;
    private readonly IValidator<CreatePostRequest> _validator;

    public PostService(IPostRepository postRepository, IValidator<CreatePostRequest> validator)
    {
        _postRepository = postRepository;
        _validator = validator;
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
        if (request.MediaFiles != null && request.MediaFiles.Count > 0)
        {
            int order = 0;
            foreach (var file in request.MediaFiles)
            {
                // TODO: Upload file to Storage (e.g., Cloudinary/Azure) and get URL.
                // For now, we simulate a saved URL.
                string simulatedUrl = $"/uploads/simulated_{Guid.NewGuid()}_{file.FileName}";
                
                post.Media.Add(new PostMedia
                {
                    MediaUrl = simulatedUrl,
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
}
