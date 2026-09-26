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
    public async Task GetFeedAsync_WhenCalledWithValidRequest_ReturnsPagedResultAndCalculatesTotalPages()
    {
        // Arrange
        var request = new GetFeedRequest { PageIndex = 2, PageSize = 3 };
        var mockItems = new List<PostFeedProjection>
        {
            new PostFeedProjection { Id = 1, Title = "Test 1" },
            new PostFeedProjection { Id = 2, Title = "Test 2" },
            new PostFeedProjection { Id = 3, Title = "Test 3" }
        };

        _mockPostRepository.Setup(r => r.GetFeedAsync(2, 3, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((10, mockItems)); // Total count is 10, page size 3 -> Total Pages should be 4

        // Act
        var result = await _postService.GetFeedAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.TotalItems);
        Assert.Equal(4, result.TotalPages);
        Assert.Equal(3, System.Linq.Enumerable.Count(result.Items));
        _mockPostRepository.Verify(r => r.GetFeedAsync(2, 3, null, null, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetFeedAsync_WhenPaginationParamsAreInvalid_ClampsToValidValues()
    {
        // Arrange
        var request = new GetFeedRequest { PageIndex = -5, PageSize = 999 };
        
        _mockPostRepository.Setup(r => r.GetFeedAsync(It.IsAny<int>(), It.IsAny<int>(), null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, new List<PostFeedProjection>()));

        // Act
        await _postService.GetFeedAsync(request);

        // Assert
        // PageIndex should clamp to 1, PageSize should clamp to 50
        _mockPostRepository.Verify(r => r.GetFeedAsync(1, 50, null, null, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
