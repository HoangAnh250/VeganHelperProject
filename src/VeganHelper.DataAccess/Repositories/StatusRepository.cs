using VeganHelper.DataAccess.Data;
namespace VeganHelper.DataAccess.Repositories;
public sealed class StatusRepository(AppDbContext db) : IStatusRepository
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) => db.Database.CanConnectAsync(cancellationToken);
}
