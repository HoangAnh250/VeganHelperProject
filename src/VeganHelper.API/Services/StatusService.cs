using VeganHelper.API.DTOs;
using VeganHelper.API.Repositories;
namespace VeganHelper.API.Services;
public sealed class StatusService(IStatusRepository repository) : IStatusService
{
    public async Task<StatusDto> GetAsync(CancellationToken cancellationToken) => new("VeganHelper", await repository.CanConnectAsync(cancellationToken));
}
