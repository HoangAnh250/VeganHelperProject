using VeganHelper.BLL.DTOs;
using VeganHelper.DAL.Repositories;
namespace VeganHelper.BLL.Services;
public sealed class StatusService(IStatusRepository repository) : IStatusService
{
    public async Task<StatusDto> GetAsync(CancellationToken cancellationToken) => new("VeganHelper", await repository.CanConnectAsync(cancellationToken));
}
