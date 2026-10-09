using FluentValidation;
using Moq;
using VeganHelper.BLL.DTOs.Comments;
using VeganHelper.BLL.Services;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.UnitTests;

public sealed class CommentServiceTests
{
    [Theory]
    [InlineData("Cắt quả này khó vcl", CommentModerationAction.Allow)]
    [InlineData("Cơm sườn chay với nấm", CommentModerationAction.Allow)]
    [InlineData("Óc chó", CommentModerationAction.BlockRejected)]
    [InlineData("ĐMM", CommentModerationAction.BlockRejected)]
    [InlineData("Địt mẹ mày", CommentModerationAction.HidePendingReview)]
    public void KeywordFilter_ReturnsExpectedAction(string content, CommentModerationAction expectedAction)
    {
        var result = new CommentKeywordFilter().Evaluate(content);

        Assert.Equal(expectedAction, result.Action);
    }

    [Fact]
    public async Task CreateCommentAsync_CreatesVisibleRootCommentAndTrimsContent()
    {
        var repository = CreateRepository();
        repository.Setup(x => x.IsPublishedPostAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.AddCommentAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Callback<Comment, CancellationToken>((comment, _) => comment.Id = 100)
            .Returns(Task.CompletedTask);
        repository.Setup(x => x.GetCommentProjectionAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentProjection
            {
                Id = 100,
                PostId = 10,
                UserId = 7,
                Content = "A useful comment",
                Status = "visible",
                AuthorName = "Member"
            });

        var service = CreateService(repository);
        var result = await service.CreateCommentAsync(10, 7, new CreateCommentRequest
        {
            Content = "  A useful comment  "
        });

        Assert.Equal(100, result.Id);
        Assert.Equal(1, result.Depth);
        Assert.Equal("visible", result.Status);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateCommentAsync_HardBlockedKeywordDoesNotPersistComment()
    {
        var repository = CreateRepository();
        repository.Setup(x => x.IsPublishedPostAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var service = CreateService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateCommentAsync(10, 7, new CreateCommentRequest
        {
            Content = "Óc chó"
        }));

        repository.Verify(x => x.AddCommentAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateCommentAsync_HiddenKeywordPersistsHiddenCommentForReview()
    {
        var repository = CreateRepository();
        repository.Setup(x => x.IsPublishedPostAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Comment? saved = null;
        repository.Setup(x => x.AddCommentAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Callback<Comment, CancellationToken>((comment, _) =>
            {
                comment.Id = 101;
                saved = comment;
            })
            .Returns(Task.CompletedTask);
        repository.Setup(x => x.GetCommentProjectionAsync(101, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentProjection
            {
                Id = 101,
                PostId = 10,
                UserId = 7,
                Content = "Địt mẹ mày",
                Status = "hidden",
                AuthorName = "Member"
            });

        var result = await CreateService(repository).CreateCommentAsync(10, 7, new CreateCommentRequest
        {
            Content = "Địt mẹ mày"
        });

        Assert.NotNull(saved);
        Assert.Equal("hidden", saved!.Status);
        Assert.Equal("hidden", result.Status);
    }

    [Fact]
    public async Task CreateCommentAsync_RejectsContentLongerThan500Characters()
    {
        var repository = CreateRepository();
        repository.Setup(x => x.IsPublishedPostAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ValidationException>(() => CreateService(repository).CreateCommentAsync(10, 7, new CreateCommentRequest
        {
            Content = new string('a', 501)
        }));

        repository.Verify(x => x.IsPublishedPostAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateCommentAsync_RejectsReplyBeyondFourthLevel()
    {
        var repository = CreateRepository();
        repository.Setup(x => x.IsPublishedPostAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.GetCommentAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, CancellationToken _) => id switch
            {
                4 => new Comment { Id = 4, PostId = 10, ParentCommentId = 3, Status = "visible" },
                3 => new Comment { Id = 3, PostId = 10, ParentCommentId = 2, Status = "visible" },
                2 => new Comment { Id = 2, PostId = 10, ParentCommentId = 1, Status = "visible" },
                1 => new Comment { Id = 1, PostId = 10, Status = "visible" },
                _ => null
            });

        await Assert.ThrowsAsync<ArgumentException>(() => CreateService(repository).CreateCommentAsync(10, 7, new CreateCommentRequest
        {
            ParentCommentId = 4,
            Content = "A fifth level reply"
        }));

        repository.Verify(x => x.AddCommentAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCommentsAsync_DefaultsToTwoLevelsAndCanReturnFourLevels()
    {
        var repository = CreateRepository();
        repository.Setup(x => x.IsPublishedPostAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.GetVisibleCommentsAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CommentProjection>
            {
                new() { Id = 1, PostId = 10, UserId = 1, Content = "Root", Status = "visible", CreatedAt = DateTime.UtcNow.AddMinutes(-5) },
                new() { Id = 2, PostId = 10, UserId = 2, ParentCommentId = 1, Content = "Reply 1", Status = "visible", CreatedAt = DateTime.UtcNow.AddMinutes(-4) },
                new() { Id = 3, PostId = 10, UserId = 3, ParentCommentId = 2, Content = "Reply 2", Status = "visible", CreatedAt = DateTime.UtcNow.AddMinutes(-3) },
                new() { Id = 4, PostId = 10, UserId = 4, ParentCommentId = 3, Content = "Reply 3", Status = "visible", CreatedAt = DateTime.UtcNow.AddMinutes(-2) }
            });

        var service = CreateService(repository);
        var preview = await service.GetCommentsAsync(10, null);
        var full = await service.GetCommentsAsync(10, null, 4);

        Assert.Single(preview);
        Assert.Single(preview[0].Replies);
        Assert.Empty(preview[0].Replies[0].Replies);
        Assert.Single(full[0].Replies[0].Replies[0].Replies);
        Assert.Equal(4, full[0].Replies[0].Replies[0].Replies[0].Depth);
    }

    private static Mock<ICommentRepository> CreateRepository() => new();

    private static CommentService CreateService(Mock<ICommentRepository> repository) =>
        new(repository.Object, new CreateCommentRequestValidator(), new CommentKeywordFilter());
}
