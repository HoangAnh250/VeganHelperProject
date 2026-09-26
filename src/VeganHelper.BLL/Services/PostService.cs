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
}
