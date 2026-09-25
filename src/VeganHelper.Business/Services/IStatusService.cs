using VeganHelper.Business.DTOs;
namespace VeganHelper.Business.Services;
public interface IStatusService { Task<StatusDto> GetAsync(CancellationToken cancellationToken); }
