namespace VeganHelper.DAL.Repositories;
public interface IStatusRepository { Task<bool> CanConnectAsync(CancellationToken cancellationToken); }
