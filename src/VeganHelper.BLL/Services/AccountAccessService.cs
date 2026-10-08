using VeganHelper.BLL.Contracts;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class AccountAccessService(IAuthRepository repository, TimeProvider clock) : IAccountAccessService
{
    public Task<string?> GetSessionRoleAsync(long userId, int tokenVersion, CancellationToken ct) =>
        repository.FindSessionRoleAsync(userId, tokenVersion, clock.GetUtcNow().UtcDateTime, ct);
}
