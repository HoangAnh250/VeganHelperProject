using VeganHelper.BLL.DTOs;
namespace VeganHelper.BLL.Contracts;
public interface IStatusService { Task<StatusDto> GetAsync(CancellationToken cancellationToken); }
