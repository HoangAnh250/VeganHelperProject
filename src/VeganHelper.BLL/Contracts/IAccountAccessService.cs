namespace VeganHelper.BLL.Contracts;

public interface IAccountAccessService
{
    Task<string?> GetSessionRoleAsync(long userId, int tokenVersion, CancellationToken ct);
}
