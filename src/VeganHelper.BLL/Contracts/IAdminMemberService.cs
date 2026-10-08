using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;

namespace VeganHelper.BLL.Contracts;

public interface IAdminMemberService
{
    Task<PagedResult<AdminMemberDto>> ListAsync(AdminActor actor, AdminMemberListRequest request, CancellationToken ct);
    Task<AdminMemberDto> GetAsync(AdminActor actor, long id, CancellationToken ct);
    Task<AdminMemberDto> BanAsync(AdminActor actor, long id, BanMemberRequest request, CancellationToken ct);
    Task<AdminMemberDto> UnbanAsync(AdminActor actor, long id, UnbanMemberRequest request, CancellationToken ct);
}
