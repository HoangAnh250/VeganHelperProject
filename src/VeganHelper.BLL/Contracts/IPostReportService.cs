using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;
using VeganHelper.BLL.DTOs.Posts;

namespace VeganHelper.BLL.Contracts;

public interface IPostReportService
{
    Task<PostReportReceipt> ReportAsync(long userId, long postId, CreatePostReportRequest request, CancellationToken ct);
    Task<PagedResult<AdminPostReportDto>> ListAsync(AdminActor actor, PostReportListRequest request, CancellationToken ct);
    Task<AdminPostReportDetailDto> GetAsync(AdminActor actor, long id, CancellationToken ct);
    Task<AdminPostReportDto> DecideAsync(AdminActor actor, long id, ModerationDecisionRequest request, CancellationToken ct);
}
