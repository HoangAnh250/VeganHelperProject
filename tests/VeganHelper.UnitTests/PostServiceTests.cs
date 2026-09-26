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
    private readonly Mock<IValidator<UpdatePostRequest>> _mockUpdateValidator;
    private readonly PostService _postService;

    public PostServiceTests()
    {
        _mockPostRepository = new Mock<IPostRepository>();
        _mockValidator = new Mock<IValidator<CreatePostRequest>>();
        _mockUpdateValidator = new Mock<IValidator<UpdatePostRequest>>();
        _postService = new PostService(_mockPostRepository.Object, _mockValidator.Object, _mockUpdateValidator.Object);
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

    [Fact]
    public async Task UpdatePostAsync_WhenUserIsNotAuthor_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var request = new UpdatePostRequest { Title = "Valid" };
        var post = new Post { Id = 1, AuthorId = 10 };
        long wrongAuthorId = 99;

        _mockUpdateValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _mockPostRepository.Setup(r => r.GetPostForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _postService.UpdatePostAsync(1, request, wrongAuthorId));
        Assert.Contains("authorized", ex.Message);
        
        _mockPostRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePostAsync_WhenRequestIsValid_UpdatesFieldsAndReturnsPendingReview()
    {
        // Arrange
        var request = new UpdatePostRequest 
        { 
            Title = "Updated Title",
            CategoryId = 2,
            Content = "Updated Content"
        };
        
        var post = new Post 
        { 
            Id = 1, 
            AuthorId = 10,
            Title = "Old Title",
            Status = "published",
            PostCategories = new List<PostCategory>(),
            Media = new List<PostMedia> { new PostMedia { Id = 1, MediaUrl = "old.jpg", IsPrimary = true } }
        };
        long authorId = 10;

        _mockUpdateValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _mockPostRepository.Setup(r => r.GetPostForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _mockPostRepository.Setup(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockPostRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockPostRepository.Setup(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _postService.UpdatePostAsync(1, request, authorId);

        // Assert
        Assert.Equal("Updated Title", post.Title);
        Assert.Equal("Updated Content", post.Content);
        Assert.Equal("pending_review", post.Status);
        Assert.Single(post.PostCategories);
        Assert.Equal(2, post.PostCategories.First().CategoryId);

        _mockPostRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockPostRepository.Verify(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
