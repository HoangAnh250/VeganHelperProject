using VeganHelper.BLL.DTOs;
namespace VeganHelper.BLL.Services;
public interface IStatusService { Task<StatusDto> GetAsync(CancellationToken cancellationToken); }
