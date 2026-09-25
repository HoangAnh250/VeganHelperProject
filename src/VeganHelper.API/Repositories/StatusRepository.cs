using VeganHelper.API.Data;
namespace VeganHelper.API.Repositories;
public sealed class StatusRepository(AppDbContext db) : IStatusRepository
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) => db.Database.CanConnectAsync(cancellationToken);
}
