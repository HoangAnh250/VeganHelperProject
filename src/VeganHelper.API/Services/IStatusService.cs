using VeganHelper.API.DTOs;
namespace VeganHelper.API.Services;
public interface IStatusService { Task<StatusDto> GetAsync(CancellationToken cancellationToken); }
