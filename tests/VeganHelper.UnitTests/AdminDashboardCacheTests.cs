using Moq;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;
using VeganHelper.BLL.Services;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.UnitTests;

public sealed class AdminDashboardCacheTests
{
    private readonly AdminActor actor = new(1, null, "cache-test");
    private static readonly DashboardRangeRequest Range = new() { From = new(2020, 1, 1), To = new(2020, 1, 7) };

    [Fact]
    public async Task RepeatedReads_ReuseStatisticsButAuthorizeAndAuditEveryRequest()
    {
        using var cache = new AdminDashboardCache();
        var repository = new Mock<IAdminDashboardRepository>(MockBehavior.Strict);
        repository.Setup(r => r.OverviewAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DashboardOverviewProjection(10, 2, 1, 5, 3, 1, 1, 1, 2));
        var audit = new GuardedAudit();
        var service = new AdminDashboardService(repository.Object, audit, TimeProvider.System, cache);
        var first = await service.OverviewAsync(actor, Range, default);
        var second = await service.OverviewAsync(actor, Range, default);
        Assert.Same(first, second);
        Assert.Equal(2, audit.Calls);
        repository.Verify(r => r.OverviewAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        audit.Allowed = false;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.OverviewAsync(actor, Range, default));
        Assert.Equal(3, audit.Calls);
    }

    [Fact]
    public async Task Cache_SeparatesResourcesAndFilters()
    {
        using var cache = new AdminDashboardCache();
        var repository = new Mock<IAdminDashboardRepository>();
        repository.Setup(r => r.ActivityAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DashboardDailyCount> { new(new DateTime(2020, 1, 1), 3) });
        repository.Setup(r => r.CategoriesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DashboardCategoryProjection(3, new List<DashboardCategoryCount> { new(1, "Food", 3) }));
        var service = new AdminDashboardService(repository.Object, new GuardedAudit(), TimeProvider.System, cache);
        foreach (var interval in new[] { "day", "week", "day" })
            await service.ActivityAsync(actor, new() { From = Range.From, To = Range.To, Interval = interval }, default);
        foreach (var limit in new[] { 1, 3, 1 })
            await service.CategoriesAsync(actor, new() { From = Range.From, To = Range.To, Limit = limit }, default);
        await service.ActivityAsync(actor, new() { From = Range.From, To = new(2020, 1, 8), Interval = "day" }, default);
        repository.Verify(r => r.ActivityAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        repository.Verify(r => r.CategoriesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task FailedOrCancelledLoads_AreNotCached()
    {
        using var cache = new AdminDashboardCache();
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync<object>("key", _ => throw new InvalidOperationException(), default));
        var calls = 0;
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetAsync<object>("key", _ =>
        {
            calls++;
            cancellation.Cancel();
            return Task.FromResult(new object());
        }, cancellation.Token));
        var expected = new object();
        var result = await cache.GetAsync("key", _ => { calls++; return Task.FromResult(expected); }, default);
        Assert.Same(expected, result);
        Assert.Equal(2, calls);
    }

    private sealed class GuardedAudit : IAdminAuditService
    {
        public bool Allowed = true;
        public int Calls;
        public Task<bool> IsActiveAdminAsync(long userId, CancellationToken ct) => Task.FromResult(Allowed);
        public async Task<T> ExecuteAsync<T>(AdminActor actor, string action, string targetType, string? targetId,
            Func<CancellationToken, Task<T>> operation, Func<T, string?>? targetIdFromResult, CancellationToken ct)
        {
            Calls++;
            if (!Allowed) throw new UnauthorizedAccessException();
            return await operation(ct);
        }
        public Task<PagedResult<AdminAuditLogDto>> ListAsync(AdminActor actor, AdminAuditListRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<AdminAuditLogDto> GetAsync(AdminActor actor, long id, CancellationToken ct) => throw new NotSupportedException();
    }
}
