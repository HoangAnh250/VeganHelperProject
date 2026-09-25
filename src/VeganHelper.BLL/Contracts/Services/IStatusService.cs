using VeganHelper.BLL.DTOs;
namespace VeganHelper.BLL.Contracts.Services;
public interface IStatusService { Task<StatusDto> GetAsync(CancellationToken cancellationToken); }
