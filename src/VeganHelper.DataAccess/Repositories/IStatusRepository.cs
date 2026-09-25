namespace VeganHelper.DataAccess.Repositories;
public interface IStatusRepository { Task<bool> CanConnectAsync(CancellationToken cancellationToken); }
