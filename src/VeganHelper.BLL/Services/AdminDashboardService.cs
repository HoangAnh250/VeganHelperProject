using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs.Admin;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class AdminDashboardService(IAdminDashboardRepository repository, IAdminAuditService audit, TimeProvider clock,
    AdminDashboardCache cache) : IAdminDashboardService
{
    public Task<DashboardOverviewDto> OverviewAsync(AdminActor actor, DashboardRangeRequest request, CancellationToken ct)
    {
        var (from, to, start, until) = Range(request);
        return audit.ExecuteAsync(actor, "dashboard.overview", "dashboard", null, token => cache.GetAsync(
            $"overview:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}", async cacheToken =>
        {
            var p = await repository.OverviewAsync(start, until, start.AddDays(-(to.DayNumber - from.DayNumber + 1)), cacheToken);
            return new DashboardOverviewDto(from, to,
                new(p.Members, p.NewMembers, p.PreviousNewMembers, p.PreviousNewMembers == 0 ? null
                    : decimal.Round((p.NewMembers - p.PreviousNewMembers) * 100m / p.PreviousNewMembers, 2)),
                new(p.PublishedPosts, p.CreatedPosts), new(p.AiPosts, p.ReportedPosts, p.ReportedComments, p.PendingContent));
        }, token), null, ct);
    }

    public Task<DashboardActivityDto> ActivityAsync(AdminActor actor, DashboardActivityRequest request, CancellationToken ct)
    {
        var (from, to, start, until) = Range(request);
        var interval = request.Interval?.Trim().ToLowerInvariant();
        if (interval is not ("day" or "week" or "month")) throw new ArgumentException("Interval must be day, week, or month.");
        return audit.ExecuteAsync(actor, "dashboard.activity", "dashboard", null, token => cache.GetAsync(
            $"activity:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}:{interval}", async cacheToken =>
        {
            var daily = await repository.ActivityAsync(start, until, cacheToken);
            var counts = daily.GroupBy(d => Bucket(DateOnly.FromDateTime(d.Day), interval))
                .ToDictionary(g => g.Key, g => g.Sum(d => d.Count));
            var periods = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
                .Select(offset => Bucket(from.AddDays(offset), interval)).Distinct();
            return new DashboardActivityDto(from, to, interval,
                periods.Select(day => new DashboardActivityItemDto(day, counts.GetValueOrDefault(day))).ToList());
        }, token), null, ct);
    }

    public Task<DashboardCategoriesDto> CategoriesAsync(AdminActor actor, DashboardCategoriesRequest request, CancellationToken ct)
    {
        var (from, to, start, until) = Range(request);
        if (request.Limit is < 1 or > 10) throw new ArgumentException("Limit must be between 1 and 10.");
        return audit.ExecuteAsync(actor, "dashboard.categories", "dashboard", null, token => cache.GetAsync(
            $"categories:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}:{request.Limit}", async cacheToken =>
        {
            var p = await repository.CategoriesAsync(start, until, request.Limit, cacheToken);
            return new DashboardCategoriesDto(from, to, p.TotalPosts, p.Items.Select(item =>
                new DashboardCategoryDto(item.CategoryId, item.Name, item.Count,
                    p.TotalPosts == 0 ? 0 : decimal.Round(item.Count * 100m / p.TotalPosts, 2))).ToList());
        }, token), null, ct);
    }

    private (DateOnly From, DateOnly To, DateTime Start, DateTime Until) Range(DashboardRangeRequest request)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var to = request.To ?? today;
        if (to.Year < 2000 || to > today)
            throw new ArgumentException("End date must be between year 2000 and today (UTC).");
        var from = request.From ?? to.AddDays(-29);
        if (from.Year < 2000 || from > to || to > today || to.DayNumber - from.DayNumber >= 366)
            throw new ArgumentException("Choose an ordered UTC date range of at most 366 days, from year 2000 through today.");
        return (from, to, from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    }

    private static DateOnly Bucket(DateOnly day, string interval) => interval switch
    {
        "month" => new(day.Year, day.Month, 1),
        "week" => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
        _ => day
    };
}
