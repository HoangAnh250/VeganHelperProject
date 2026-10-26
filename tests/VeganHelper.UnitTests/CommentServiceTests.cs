using FluentValidation;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs.Comments;
using VeganHelper.BLL.Exceptions;
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
    [InlineData("You are an idiot", CommentModerationAction.BlockRejected)]
    [InlineData("FUCK YOU!", CommentModerationAction.BlockRejected)]
    [InlineData("What the fuck?", CommentModerationAction.HidePendingReview)]
    [InlineData("This is bullshit", CommentModerationAction.HidePendingReview)]
    [InlineData("Damn, this recipe is hard", CommentModerationAction.Allow)]
    [InlineData("Use shiitake mushrooms", CommentModerationAction.Allow)]
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

        var notifications = new Mock<INotificationPublisher>();
        var service = CreateService(repository, notifications);
        var result = await service.CreateCommentAsync(10, 7, new CreateCommentRequest
        {
            Content = "  A useful comment  "
        });

        Assert.Equal(100, result.Id);
        Assert.Equal(1, result.Depth);
        Assert.Equal("visible", result.Status);
        notifications.Verify(x => x.NotifyCommentAsync(100, It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task UpdateCommentAsync_AllowsOwnerAndReevaluatesModeration()
    {
        var repository = CreateRepository();
        var comment = new Comment
        {
            Id = 100,
            PostId = 10,
            UserId = 7,
            Content = "Old comment",
            Status = "visible"
        };
        repository.Setup(x => x.IsPublishedPostAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.GetCommentForUpdateAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(comment);
        repository.Setup(x => x.GetCommentProjectionAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new CommentProjection
            {
                Id = comment.Id,
                PostId = comment.PostId,
                UserId = comment.UserId,
                Content = comment.Content,
                Status = comment.Status,
                AuthorName = "Member"
            });

        var result = await CreateService(repository).UpdateCommentAsync(10, 100, 7, new UpdateCommentRequest
        {
            Content = "  Updated comment  "
        });

        Assert.Equal("Updated comment", comment.Content);
        Assert.Equal("visible", comment.Status);
        Assert.Equal("Updated comment", result.Content);
        Assert.NotNull(comment.UpdatedAt);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCommentAsync_RejectsAnotherMember()
    {
        var repository = CreateRepository();
        repository.Setup(x => x.IsPublishedPostAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.GetCommentForUpdateAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Comment { Id = 100, PostId = 10, UserId = 7, Status = "visible" });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateService(repository).UpdateCommentAsync(
            10,
            100,
            8,
            new UpdateCommentRequest { Content = "Attempted edit" }));

        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCommentAsync_AdminCannotEditAnotherMemberComment()
    {
        var repository = CreateRepository();
        repository.Setup(x => x.IsPublishedPostAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.GetCommentForUpdateAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Comment { Id = 100, PostId = 10, UserId = 7, Status = "visible" });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateService(repository).UpdateCommentAsync(
            10,
            100,
            8,
            new UpdateCommentRequest { Content = "Attempted admin edit" }));

        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteCommentAsync_SoftDeletesCommentAndPreservesRow()
    {
        var repository = CreateRepository();
        var comment = new Comment
        {
            Id = 100,
            PostId = 10,
            UserId = 7,
            Content = "Comment",
            Status = "visible"
        };
        repository.Setup(x => x.GetCommentForUpdateAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(comment);

        await CreateService(repository).DeleteCommentAsync(10, 100, 7, false);

        Assert.True(comment.IsDeleted);
        Assert.NotNull(comment.DeletedAt);
        Assert.Equal("hidden", comment.Status);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCommentAsync_AllowsAdministratorToHideComment()
    {
        var repository = CreateRepository();
        var comment = new Comment
        {
            Id = 100,
            PostId = 10,
            UserId = 7,
            Content = "Comment",
            Status = "visible"
        };
        repository.Setup(x => x.GetCommentForUpdateAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(comment);

        var reason = new string('x', DeleteCommentRequest.MaxReasonLength);
        await CreateService(repository).DeleteCommentAsync(10, 100, 8, true, new DeleteCommentRequest
        {
            Reason = reason
        });

        Assert.True(comment.IsDeleted);
        Assert.Equal("hidden", comment.Status);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCommentAsync_PostAuthorCanDeleteAnotherUsersRootWithoutReasonOrNotification()
    {
        var repository = CreateRepository();
        var transaction = CreateTransaction();
        var root = new Comment { Id = 100, PostId = 10, UserId = 7, Content = "Root", Status = "visible" };
        var reply = new Comment { Id = 101, PostId = 10, UserId = 8, ParentCommentId = 100, Content = "Reply", Status = "visible" };
        repository.Setup(x => x.GetCommentForUpdateAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(root);
        repository.Setup(x => x.GetPostAuthorIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(20);
        repository.Setup(x => x.GetCommentsForUpdateAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment> { root, reply });
        repository.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(transaction.Object);
        var notifications = new Mock<INotificationPublisher>();

        await CreateService(repository, notifications).DeleteCommentAsync(10, 100, 20, false);

        Assert.True(root.IsDeleted);
        Assert.True(reply.IsDeleted);
        notifications.Verify(x => x.NotifyCommentDeletionAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCommentAsync_AdminCanDeleteOwnCommentWithoutReasonOrNotification()
    {
        var repository = CreateRepository();
        var transaction = CreateTransaction();
        var comment = new Comment
        {
            Id = 100, PostId = 10, UserId = 8, ParentCommentId = 90,
            Content = "My comment", Status = "visible"
        };
        repository.Setup(x => x.GetCommentForUpdateAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(comment);
        repository.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(transaction.Object);
        var notifications = new Mock<INotificationPublisher>();

        await CreateService(repository, notifications).DeleteCommentAsync(10, 100, 8, true);

        Assert.True(comment.IsDeleted);
        notifications.Verify(x => x.NotifyCommentDeletionAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCommentAsync_AdminMustProvideReasonForAnotherUsersComment()
    {
        var repository = CreateRepository();
        repository.Setup(x => x.GetCommentForUpdateAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Comment { Id = 100, PostId = 10, UserId = 7, Content = "Comment", Status = "visible" });

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService(repository).DeleteCommentAsync(10, 100, 8, true));

        Assert.Contains("reason is required", exception.Message);
        repository.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteCommentAsync_AdminPostAuthorRequiresReasonButDoesNotNotifyCommentOwner()
    {
        var repository = CreateRepository();
        var transaction = CreateTransaction();
        var comment = new Comment { Id = 100, PostId = 10, UserId = 7, Content = "Comment", Status = "visible" };
        repository.Setup(x => x.GetCommentForUpdateAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(comment);
        repository.Setup(x => x.GetPostAuthorIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(8);
        repository.Setup(x => x.GetCommentsForUpdateAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { comment });
        repository.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(transaction.Object);
        var notifications = new Mock<INotificationPublisher>();

        await CreateService(repository, notifications).DeleteCommentAsync(
            10, 100, 8, true, new DeleteCommentRequest { Reason = "Removed by the post author" });

        Assert.True(comment.IsDeleted);
        notifications.Verify(x => x.NotifyCommentDeletionAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCommentAsync_UnrelatedMemberCannotDeleteAnotherUsersComment()
    {
        var repository = CreateRepository();
        var comment = new Comment { Id = 100, PostId = 10, UserId = 7, Content = "Comment", Status = "visible" };
        repository.Setup(x => x.GetCommentForUpdateAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(comment);
        repository.Setup(x => x.GetPostAuthorIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(20);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService(repository).DeleteCommentAsync(10, 100, 99, false));

        repository.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void DeleteCommentRequest_AllowsAnEmptyReasonUntilTheActorIsAuthorized()
    {
        var request = new DeleteCommentRequest { Reason = string.Empty };
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        Assert.True(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            request,
            new System.ComponentModel.DataAnnotations.ValidationContext(request),
            results,
            validateAllProperties: true));
    }

    [Fact]
    public async Task DeleteCommentAsync_RejectsAdministratorReasonOver500Characters()
    {
        var repository = CreateRepository();
        repository.Setup(x => x.GetCommentForUpdateAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Comment
            {
                Id = 100,
                PostId = 10,
                UserId = 7,
                Content = "Comment",
                Status = "visible"
            });

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService(repository).DeleteCommentAsync(
                10,
                100,
                8,
                true,
                new DeleteCommentRequest { Reason = new string('x', DeleteCommentRequest.MaxReasonLength + 1) }));

        Assert.Contains("500", exception.Message);
        repository.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteCommentAsync_RootCommentSoftDeletesEntireThreadWithoutNotificationForOwnerDelete()
    {
        var repository = CreateRepository();
        var transaction = CreateTransaction();
        var root = new Comment { Id = 100, PostId = 10, UserId = 7, Content = "Root", Status = "visible" };
        var reply = new Comment { Id = 101, PostId = 10, UserId = 8, ParentCommentId = 100, Content = "Reply", Status = "visible" };
        var nestedReply = new Comment { Id = 102, PostId = 10, UserId = 9, ParentCommentId = 101, Content = "Nested reply", Status = "visible" };
        var unrelatedRoot = new Comment { Id = 200, PostId = 10, UserId = 10, Content = "Other root", Status = "visible" };

        repository.Setup(x => x.GetCommentForUpdateAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(root);
        repository.Setup(x => x.GetCommentsForUpdateAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment> { root, reply, nestedReply, unrelatedRoot });
        repository.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(transaction.Object);

        var notifications = new Mock<INotificationPublisher>();
        await CreateService(repository, notifications).DeleteCommentAsync(10, 100, 7, false);

        Assert.True(root.IsDeleted);
        Assert.True(reply.IsDeleted);
        Assert.True(nestedReply.IsDeleted);
        Assert.False(unrelatedRoot.IsDeleted);
        notifications.Verify(x => x.NotifyCommentDeletionAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCommentAsync_NestedAdministratorDeleteKeepsRepliesAndNotifiesSelectedOwner()
    {
        var repository = CreateRepository();
        var transaction = CreateTransaction();
        var target = new Comment { Id = 101, PostId = 10, UserId = 8, ParentCommentId = 100, Content = "Reply", Status = "visible" };
        var reply = new Comment { Id = 102, PostId = 10, UserId = 9, ParentCommentId = 101, Content = "Nested reply", Status = "visible" };

        repository.Setup(x => x.GetCommentForUpdateAsync(101, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        repository.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(transaction.Object);
        var notifications = new Mock<INotificationPublisher>();
        notifications.Setup(x => x.NotifyCommentDeletionAsync(
                8, 101, 10, "Reply", "Community guideline violation", It.IsAny<CancellationToken>()))
            .ReturnsAsync(500L);

        await CreateService(repository, notifications).DeleteCommentAsync(
            10,
            101,
            99,
            true,
            new DeleteCommentRequest { Reason = "Community guideline violation" });

        Assert.True(target.IsDeleted);
        Assert.False(reply.IsDeleted);
        notifications.Verify(x => x.NotifyCommentDeletionAsync(
            8, 101, 10, "Reply", "Community guideline violation", It.IsAny<CancellationToken>()), Times.Once);
        transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCommentsAsync_DeletedNestedParentKeepsReplyAndExposesReplyContext()
    {
        var repository = CreateRepository();
        repository.Setup(x => x.IsPublishedPostAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.GetVisibleCommentsAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CommentProjection>
            {
                new() { Id = 100, PostId = 10, UserId = 7, AuthorName = "Root", Content = "Root", Status = "visible" },
                new() { Id = 101, PostId = 10, UserId = 8, AuthorName = "Deleted author", ParentCommentId = 100, Content = "Deleted", Status = "hidden", IsDeleted = true },
                new() { Id = 102, PostId = 10, UserId = 9, AuthorName = "Reply author", ParentCommentId = 101, Content = "Still visible", Status = "visible" }
            });

        var result = await CreateService(repository).GetCommentsAsync(10, null, 4);

        var deleted = result[0].Replies[0];
        var reply = deleted.Replies[0];
        Assert.Equal("This comment has been deleted.", deleted.Content);
        Assert.Equal("Deleted author", reply.ReplyToDisplayName);
        Assert.True(reply.IsReplyToDeleted);
        Assert.Equal("Still visible", reply.Content);
    }

    [Fact]
    public async Task ReportCommentAsync_FirstReportKeepsCommentVisible()
    {
        var repository = CreateRepository();
        var comment = new Comment { Id = 100, PostId = 10, UserId = 7, Status = "visible" };
        var transaction = CreateTransaction();
        repository.Setup(x => x.IsPublishedPostAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(transaction.Object);
        repository.Setup(x => x.GetCommentForReportAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(comment);
        repository.Setup(x => x.HasPendingUserReportAsync(100, 8, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repository.Setup(x => x.AddCommentReportAsync(It.IsAny<Flag>(), It.IsAny<CancellationToken>()))
            .Callback<Flag, CancellationToken>((report, _) => report.Id = 500)
            .Returns(Task.CompletedTask);
        repository.Setup(x => x.CountPendingUserReportsAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await CreateService(repository).ReportCommentAsync(10, 100, 8, new ReportCommentRequest
        {
            Reason = "Off-topic content"
        });

        Assert.Equal(500, result.ReportId);
        Assert.Equal(1, result.PendingReportCount);
        Assert.False(result.CommentHidden);
        Assert.Equal("visible", comment.Status);
        transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportCommentAsync_ThirdUniqueReportHidesComment()
    {
        var repository = CreateRepository();
        var comment = new Comment { Id = 100, PostId = 10, UserId = 7, Status = "visible" };
        var transaction = CreateTransaction();
        repository.Setup(x => x.IsPublishedPostAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(transaction.Object);
        repository.Setup(x => x.GetCommentForReportAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(comment);
        repository.Setup(x => x.HasPendingUserReportAsync(100, 9, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repository.Setup(x => x.AddCommentReportAsync(It.IsAny<Flag>(), It.IsAny<CancellationToken>()))
            .Callback<Flag, CancellationToken>((report, _) => report.Id = 501)
            .Returns(Task.CompletedTask);
        repository.Setup(x => x.CountPendingUserReportsAsync(100, It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var result = await CreateService(repository).ReportCommentAsync(10, 100, 9, new ReportCommentRequest
        {
            Reason = "Inappropriate content"
        });

        Assert.True(result.CommentHidden);
        Assert.Equal(3, result.PendingReportCount);
        Assert.Equal("hidden", comment.Status);
        Assert.NotNull(comment.UpdatedAt);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ReportCommentAsync_RejectsDuplicateReportFromSameUser()
    {
        var repository = CreateRepository();
        var transaction = CreateTransaction();
        repository.Setup(x => x.IsPublishedPostAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(transaction.Object);
        repository.Setup(x => x.GetCommentForReportAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Comment { Id = 100, PostId = 10, UserId = 7, Status = "visible" });
        repository.Setup(x => x.HasPendingUserReportAsync(100, 8, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => CreateService(repository).ReportCommentAsync(
            10,
            100,
            8,
            new ReportCommentRequest { Reason = "Duplicate report" }));

        repository.Verify(x => x.AddCommentReportAsync(It.IsAny<Flag>(), It.IsAny<CancellationToken>()), Times.Never);
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

    private static Mock<IDbContextTransaction> CreateTransaction()
    {
        var transaction = new Mock<IDbContextTransaction>();
        transaction.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        transaction.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return transaction;
    }

    private static CommentService CreateService(
        Mock<ICommentRepository> repository,
        Mock<INotificationPublisher>? notifications = null)
    {
        notifications ??= new Mock<INotificationPublisher>();
        notifications.Setup(x => x.NotifyCommentDeletionAsync(
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)1);

        return new(
            repository.Object,
            new CreateCommentRequestValidator(),
            new UpdateCommentRequestValidator(),
            new ReportCommentRequestValidator(),
            new CommentKeywordFilter(),
            notifications.Object);
    }
}
