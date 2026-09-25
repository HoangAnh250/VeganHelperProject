using VeganHelper.DAL.Entities;

namespace VeganHelper.BLL.Contracts.Services;

public interface IJwtTokenService
{
    AccessTokenResult CreateAccessToken(User user, string roleName, DateTime now);
}

public sealed record AccessTokenResult(string Token, DateTime ExpiresAt);
