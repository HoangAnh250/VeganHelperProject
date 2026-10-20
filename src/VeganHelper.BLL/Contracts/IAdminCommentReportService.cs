using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;

namespace VeganHelper.BLL.Contracts;

public interface IAdminCommentReportService
{
    Task<PagedResult<AdminCommentReportDto>> ListAsync(AdminActor actor, AdminCommentReportListRequest request, CancellationToken ct);
    Task<AdminCommentReportDto> GetAsync(AdminActor actor, long id, CancellationToken ct);
    Task<AdminCommentReportDto> DecideAsync(AdminActor actor, long id, AdminCommentReportDecisionRequest request, CancellationToken ct);
}
