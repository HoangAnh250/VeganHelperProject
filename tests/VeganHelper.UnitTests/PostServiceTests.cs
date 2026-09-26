namespace VeganHelper.UnitTests;

using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VeganHelper.BLL.DTOs.Posts;
using VeganHelper.BLL.Services;
using VeganHelper.DAL.Models;
using VeganHelper.DAL.Repositories;
using Xunit;

public class PostServiceTests
{
    private readonly Mock<IPostRepository> _mockPostRepository;
    private readonly Mock<IValidator<CreatePostRequest>> _mockValidator;
    private readonly PostService _postService;

    public PostServiceTests()
    {
        _mockPostRepository = new Mock<IPostRepository>();
        _mockValidator = new Mock<IValidator<CreatePostRequest>>();
        _postService = new PostService(_mockPostRepository.Object, _mockValidator.Object);
    }

    [Fact]
    public async Task CreatePostAsync_WhenRequestIsValid_CreatesPostAndCommitsTransaction()
    {
        // Arrange
        var request = new CreatePostRequest
        {
            Title = "Valid Post",
            PostType = "recipe",
            CategoryId = 1,
            Content = "Test content",
            IngredientsJson = "[{\"IngredientId\": 1, \"Quantity\": 2.5, \"Unit\": \"kg\"}]"
        };
        long authorId = 100;
        var cancellationToken = CancellationToken.None;

        _mockValidator.Setup(v => v.ValidateAsync(It.IsAny<CreatePostRequest>(), cancellationToken))
            .ReturnsAsync(new ValidationResult());
        _mockValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), cancellationToken))
            .ReturnsAsync(new ValidationResult());

        _mockPostRepository.Setup(r => r.BeginTransactionAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        _mockPostRepository.Setup(r => r.CreatePostAsync(It.IsAny<Post>(), cancellationToken))
            .ReturnsAsync(new Post { Id = 123 });

        _mockPostRepository.Setup(r => r.CommitTransactionAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _postService.CreatePostAsync(request, authorId, cancellationToken);

        // Assert
        Assert.Equal(123, result);
        _mockPostRepository.Verify(r => r.BeginTransactionAsync(cancellationToken), Times.Once);
        _mockPostRepository.Verify(r => r.CreatePostAsync(It.Is<Post>(p => p.Title == "Valid Post" && p.PostIngredients.Count == 1), cancellationToken), Times.Once);
        _mockPostRepository.Verify(r => r.CommitTransactionAsync(cancellationToken), Times.Once);
        _mockPostRepository.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatePostAsync_WhenValidationFails_ThrowsValidationException()
    {
        // Arrange
        var request = new CreatePostRequest { Title = "" };
        var validationFailure = new ValidationFailure("Title", "Title is required");
        
        _mockValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new ValidationException(new[] { validationFailure }));

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _postService.CreatePostAsync(request, 1));
        
        _mockPostRepository.Verify(r => r.CreatePostAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatePostAsync_WhenIngredientsJsonIsInvalid_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreatePostRequest
        {
            Title = "Valid Post",
            IngredientsJson = "invalid-json"
        };

        _mockValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreatePostRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _mockValidator.Setup(v => v.ValidateAsync(It.IsAny<CreatePostRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _mockValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _postService.CreatePostAsync(request, 1));
        Assert.Contains("Invalid JSON", ex.Message);
        
        _mockPostRepository.Verify(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPostDetailAsync_WhenPostExists_ReturnsDtoAndIncrementsViewCount()
    {
        // Arrange
        long postId = 1;
        var mockPost = new Post
        {
            Id = postId,
            AuthorId = 10,
            Title = "Test Recipe",
            ViewCount = 5,
            PostCategories = new List<PostCategory> { new PostCategory { CategoryId = 2 } }
        };
        var authorName = "Chef John";

        _mockPostRepository.Setup(r => r.GetPostDetailAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((mockPost, authorName));

        // Act
        var result = await _postService.GetPostDetailAsync(postId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(postId, result.Id);
        Assert.Equal("Test Recipe", result.Title);
        Assert.Equal("Chef John", result.AuthorName);
        Assert.Equal(6, result.ViewCount); // Ensure view count was incremented locally
        Assert.Equal(2, result.CategoryId);

        _mockPostRepository.Verify(r => r.GetPostDetailAsync(postId, It.IsAny<CancellationToken>()), Times.Once);
        _mockPostRepository.Verify(r => r.IncrementViewCountAsync(postId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPostDetailAsync_WhenPostDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        long postId = 999;
        _mockPostRepository.Setup(r => r.GetPostDetailAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, string.Empty));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<VeganHelper.BLL.Exceptions.NotFoundException>(() => _postService.GetPostDetailAsync(postId));
        Assert.Contains(postId.ToString(), ex.Message);

        _mockPostRepository.Verify(r => r.IncrementViewCountAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
