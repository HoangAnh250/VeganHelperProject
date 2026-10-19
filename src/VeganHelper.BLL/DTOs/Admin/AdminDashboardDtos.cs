using System.ComponentModel.DataAnnotations;

namespace VeganHelper.BLL.DTOs.Admin;

// Inclusive calendar dates in UTC. Omitted dates default to the last 30 days.
public class DashboardRangeRequest
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}
public sealed class DashboardActivityRequest : DashboardRangeRequest
{
    public string Interval { get; init; } = "day";
}
public sealed class DashboardCategoriesRequest : DashboardRangeRequest
{
    [Range(1, 10)] public int Limit { get; init; } = 3;
}
public sealed record DashboardMembersDto(long Total, long NewInPeriod, long NewInPreviousPeriod, decimal? GrowthPercentage);
public sealed record DashboardPostsDto(long TotalPublished, long CreatedInPeriod);
public sealed record DashboardModerationDto(long AiFlaggedPosts, long ReportedPosts, long ReportedComments, long TotalPending);
public sealed record DashboardOverviewDto(DateOnly From, DateOnly To, DashboardMembersDto Members,
    DashboardPostsDto Posts, DashboardModerationDto Moderation);
public sealed record DashboardActivityItemDto(DateOnly Period, long PostCount);
public sealed record DashboardActivityDto(DateOnly From, DateOnly To, string Interval, IReadOnlyList<DashboardActivityItemDto> Items);
public sealed record DashboardCategoryDto(int CategoryId, string Name, long PostCount, decimal Percentage);
public sealed record DashboardCategoriesDto(DateOnly From, DateOnly To, long TotalCategorizedPosts, IReadOnlyList<DashboardCategoryDto> Items);
