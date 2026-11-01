using AutoMapper;
using Moq;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;
using VeganHelper.BLL.Mapping;
using VeganHelper.BLL.Services;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

using Microsoft.Extensions.Logging.Abstractions;

namespace VeganHelper.UnitTests;

public sealed class AdminCommentReportServiceTests
{
    private sealed class TestAudit : IAdminAuditService
    {
        public Task<bool> IsActiveAdminAsync(long userId, CancellationToken ct) => Task.FromResult(true);
        public Task<T> ExecuteAsync<T>(AdminActor actor, string action, string targetType, string? targetId,
            Func<CancellationToken, Task<T>> operation, Func<T, string?>? targetIdFromResult, CancellationToken ct) =>
            operation(ct);
        public Task<PagedResult<AdminAuditLogDto>> ListAsync(AdminActor actor, AdminAuditListRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<AdminAuditLogDto> GetAsync(AdminActor actor, long id, CancellationToken ct) => throw new NotSupportedException();
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    [Fact]
    public async Task DecideAsync_WhenActionIsRemove_NotifiesCommentAuthorWithReason()
    {
        var repository = new Mock<IAdminCommentReportRepository>();
        var notifications = new Mock<INotificationPublisher>();
        var audit = new TestAudit();
        var mapper = CreateMapper();
        var clock = TimeProvider.System;

        var report = new Flag
        {
            Id = 50,
            CommentId = 120,
            SourceType = "user",
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        var comment = new Comment
        {
            Id = 120,
            PostId = 300,
            UserId = 88,
            Content = "This is violating content",
            Status = "visible"
        };
        var projection = new AdminCommentReportProjection
        {
            Id = 50,
            CommentId = 120,
            PostId = 300,
            AuthorId = 88,
            AuthorName = "BadActor",
            CommentContent = "This is violating content",
            Reason = "Inappropriate language",
            Status = "resolved"
        };

        repository.Setup(r => r.LockReportAsync(50, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        repository.Setup(r => r.LockCommentAsync(120, It.IsAny<CancellationToken>())).ReturnsAsync(comment);
        repository.Setup(r => r.FindAsync(50, It.IsAny<CancellationToken>())).ReturnsAsync(projection);
        repository.Setup(r => r.ResolvePendingAsync(120, "resolved", 1, "Violates rules", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        notifications.Setup(n => n.NotifyCommentDeletionAsync(88, 120, 300, "This is violating content", "Violates rules", It.IsAny<CancellationToken>()))
            .ReturnsAsync(999L);

        var service = new AdminCommentReportService(repository.Object, audit, notifications.Object, mapper, clock);
        var actor = new AdminActor(1, "127.0.0.1", "trace-1");

        var result = await service.DecideAsync(actor, 50, new AdminCommentReportDecisionRequest
        {
            Action = "remove",
            Reason = "Violates rules"
        }, CancellationToken.None);

        Assert.Equal("resolved", result.Status);
        Assert.True(comment.IsDeleted);
        Assert.Equal("hidden", comment.Status);
        notifications.Verify(n => n.NotifyCommentDeletionAsync(88, 120, 300, "This is violating content", "Violates rules", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DecideAsync_WhenActionIsKeep_DoesNotNotifyCommentDeletion()
    {
        var repository = new Mock<IAdminCommentReportRepository>();
        var notifications = new Mock<INotificationPublisher>();
        var audit = new TestAudit();
        var mapper = CreateMapper();
        var clock = TimeProvider.System;

        var report = new Flag
        {
            Id = 51,
            CommentId = 121,
            SourceType = "user",
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        var comment = new Comment
        {
            Id = 121,
            PostId = 301,
            UserId = 89,
            Content = "Harmless comment",
            Status = "hidden"
        };
        var projection = new AdminCommentReportProjection
        {
            Id = 51,
            CommentId = 121,
            PostId = 301,
            AuthorId = 89,
            AuthorName = "GoodActor",
            CommentContent = "Harmless comment",
            Reason = "False report",
            Status = "dismissed"
        };

        repository.Setup(r => r.LockReportAsync(51, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        repository.Setup(r => r.LockCommentAsync(121, It.IsAny<CancellationToken>())).ReturnsAsync(comment);
        repository.Setup(r => r.FindAsync(51, It.IsAny<CancellationToken>())).ReturnsAsync(projection);
        repository.Setup(r => r.ResolvePendingAsync(121, "dismissed", 1, "Comment is fine", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = new AdminCommentReportService(repository.Object, audit, notifications.Object, mapper, clock);
        var actor = new AdminActor(1, "127.0.0.1", "trace-1");

        var result = await service.DecideAsync(actor, 51, new AdminCommentReportDecisionRequest
        {
            Action = "keep",
            Reason = "Comment is fine"
        }, CancellationToken.None);

        Assert.Equal("dismissed", result.Status);
        Assert.False(comment.IsDeleted);
        Assert.Equal("visible", comment.Status);
        notifications.Verify(n => n.NotifyCommentDeletionAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
