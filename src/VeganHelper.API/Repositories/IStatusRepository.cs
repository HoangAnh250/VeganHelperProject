namespace VeganHelper.API.Repositories;
public interface IStatusRepository { Task<bool> CanConnectAsync(CancellationToken cancellationToken); }
