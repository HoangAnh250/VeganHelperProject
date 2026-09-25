using VeganHelper.DAL.Data;
namespace VeganHelper.DAL.Repositories;
public sealed class StatusRepository(AppDbContext db) : IStatusRepository
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) => db.Database.CanConnectAsync(cancellationToken);
}
