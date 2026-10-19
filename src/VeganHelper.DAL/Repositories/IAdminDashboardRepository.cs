namespace VeganHelper.DAL.Repositories;

public sealed record DashboardOverviewProjection(long Members, long NewMembers, long PreviousNewMembers,
    long PublishedPosts, long CreatedPosts, long AiPosts, long ReportedPosts, long ReportedComments, long PendingContent);
public sealed record DashboardDailyCount(DateTime Day, long Count);
public sealed record DashboardCategoryCount(int CategoryId, string Name, long Count);
public sealed record DashboardCategoryProjection(long TotalPosts, IReadOnlyList<DashboardCategoryCount> Items);
public interface IAdminDashboardRepository
{
    Task<DashboardOverviewProjection> OverviewAsync(DateTime from, DateTime until, DateTime previousFrom, CancellationToken ct);
    Task<List<DashboardDailyCount>> ActivityAsync(DateTime from, DateTime until, CancellationToken ct);
    Task<DashboardCategoryProjection> CategoriesAsync(DateTime from, DateTime until, int limit, CancellationToken ct);
}
