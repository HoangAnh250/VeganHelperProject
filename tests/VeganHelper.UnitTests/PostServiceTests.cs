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
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;
using Xunit;

public class PostServiceTests
{
    private readonly Mock<IPostRepository> _mockPostRepository;
    private readonly Mock<IValidator<CreatePostRequest>> _mockValidator;

    private readonly Mock<IValidator<GetMyPostsRequest>> _mockGetMyPostsValidator;

    private readonly Mock<IValidator<UpdatePostRequest>> _mockUpdateValidator;
    private readonly Mock<VeganHelper.BLL.Services.Media.IMediaStorageService> _mockMediaStorageService;
    private readonly PostService _postService;

    public PostServiceTests()
    {
        _mockPostRepository = new Mock<IPostRepository>();
        _mockValidator = new Mock<IValidator<CreatePostRequest>>();

        _mockGetMyPostsValidator = new Mock<IValidator<GetMyPostsRequest>>();
        _mockUpdateValidator = new Mock<IValidator<UpdatePostRequest>>();
        _mockMediaStorageService = new Mock<VeganHelper.BLL.Services.Media.IMediaStorageService>();
        _postService = new PostService(
            _mockPostRepository.Object, 
            _mockValidator.Object, 
            _mockGetMyPostsValidator.Object, 
            _mockUpdateValidator.Object,
            _mockMediaStorageService.Object);
    }

    [Fact]
    public async Task CreatePost_WithNamedIngredientAndSteps_UsesResolvedIngredientId()
    {
        _mockPostRepository.Setup(r => r.GetOrCreateIngredientAsync("Đậu hũ", "", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Ingredient { Id = 42, Name = "Đậu hũ" });
        Post? saved = null;
        _mockPostRepository.Setup(r => r.CreatePostAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()))
            .Callback<Post, CancellationToken>((p, _) => saved = p)
            .ReturnsAsync((Post p, CancellationToken _) => p);

        await _postService.CreatePostAsync(new CreatePostRequest
        {
            Title = "Recipe", PostType = "recipe", CategoryId = 6, Content = "Recipe content",
            IngredientsJson = "[{\"Name\":\"Đậu hũ\"}]",
            StepsJson = "[{\"StepNumber\":1,\"Description\":\"Rửa nguyên liệu\"}]"
        }, 1);

        Assert.NotNull(saved);
        Assert.Equal(42, Assert.Single(saved.PostIngredients).IngredientId);
        Assert.Equal("Rửa nguyên liệu", Assert.Single(saved.PostSteps).Description);
    }

    [Fact]
    public async Task CreatePost_WithUnknownIngredientId_ReturnsInputError()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _postService.CreatePostAsync(new CreatePostRequest
        {
            IngredientsJson = "[{\"IngredientId\":999}]"
        }, 1));
        _mockPostRepository.Verify(r => r.CreatePostAsync(It.IsAny<Post>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockPostRepository.Verify(r => r.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("[{\"Name\":\"\"}]", "[]")]
    [InlineData("[{\"Name\":\"Tofu\",\"Quantity\":0}]", "[]")]
    [InlineData("[{\"Name\":\"Tofu\",\"Quantity\":0.0001}]", "[]")]
    [InlineData("[]", "[{\"StepNumber\":0,\"Description\":\"Cook\"}]")]
    [InlineData("[]", "[{\"StepNumber\":1,\"Description\":\"\"}]")]
    [InlineData("[]", "[{\"StepNumber\":1,\"Description\":\"Cook\"},{\"StepNumber\":1,\"Description\":\"Serve\"}]")]
    [InlineData("[null]", "[]")]
    [InlineData("[]", "null")]
    public async Task CreatePost_WithInvalidRecipeDetails_RejectsBeforeDatabaseOrUpload(string ingredients, string steps)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _postService.CreatePostAsync(new CreatePostRequest
        {
            IngredientsJson = ingredients, StepsJson = steps
        }, 1));
        _mockPostRepository.Verify(r => r.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockMediaStorageService.Verify(m => m.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()), Times.Never);
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
        _mockPostRepository.Setup(r => r.FindIngredientByIdAsync(1, cancellationToken)).ReturnsAsync(new Ingredient { Id = 1, Name = "Tofu" });

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
            Status = "published",
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

    public async Task GetMyPostsAsync_WhenCalled_ReturnsPagedResultAndMapsThumbnail()
    {
        // Arrange
        var request = new GetMyPostsRequest { PageIndex = 1, PageSize = 10 };
        long authorId = 1;
        
        var posts = new List<Post>
        {
            new Post
            {
                Id = 1,
                Title = "Test Post",
                Status = "published",
                CreatedAt = DateTime.UtcNow,
                Media = new List<PostMedia> { new PostMedia { MediaUrl = "thumb.jpg", IsPrimary = true } }
            }
        };

        _mockGetMyPostsValidator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _mockPostRepository.Setup(r => r.GetMyPostsAsync(authorId, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((posts, 1));

        // Act
        var result = await _postService.GetMyPostsAsync(authorId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("Test Post", result.Items.First().Title);
        Assert.Equal("thumb.jpg", result.Items.First().ThumbnailUrl);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task DeletePostAsync_WhenUserIsNotAuthor_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var post = new Post { Id = 1, AuthorId = 10 };
        long wrongAuthorId = 99;

        _mockPostRepository.Setup(r => r.GetPostDetailAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((post, "Author"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _postService.DeletePostAsync(1, wrongAuthorId));
        Assert.Contains("authorized", ex.Message);
        
        _mockPostRepository.Verify(r => r.DeletePostAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeletePostAsync_WhenPostDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        _mockPostRepository.Setup(r => r.GetPostDetailAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((Post?)null, ""));

        // Act & Assert
        await Assert.ThrowsAsync<VeganHelper.BLL.Exceptions.NotFoundException>(() => _postService.DeletePostAsync(1, 10));
    }

    [Fact]
    public async Task DeletePostAsync_WhenCalled_DeletesPost()
    {
        // Arrange
        var post = new Post { Id = 1, AuthorId = 10 };
        long authorId = 10;

        _mockPostRepository.Setup(r => r.GetPostDetailAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((post, "Author"));
            
        _mockPostRepository.Setup(r => r.DeletePostAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _postService.DeletePostAsync(1, authorId);

        // Assert
        _mockPostRepository.Verify(r => r.DeletePostAsync(1, It.IsAny<CancellationToken>()), Times.Once);
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
