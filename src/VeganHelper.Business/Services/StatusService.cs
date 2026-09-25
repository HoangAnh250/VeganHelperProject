using VeganHelper.Business.DTOs;
using VeganHelper.DataAccess.Repositories;
namespace VeganHelper.Business.Services;
public sealed class StatusService(IStatusRepository repository) : IStatusService
{
    public async Task<StatusDto> GetAsync(CancellationToken cancellationToken) => new("VeganHelper", await repository.CanConnectAsync(cancellationToken));
}
