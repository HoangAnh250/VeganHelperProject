using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public interface IAuthRepository
{
    Task<User?> FindUserByEmailAsync(string email, CancellationToken cancellationToken);
    Task<User?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<User?> FindUserByIdAsync(long id, CancellationToken cancellationToken);
    Task<string?> FindRoleNameAsync(int roleId, CancellationToken cancellationToken);
    Task AddUserAsync(User user, CancellationToken cancellationToken);
    Task AddEmailVerificationTokenAsync(EmailVerificationToken token, CancellationToken cancellationToken);
    Task<EmailVerificationToken?> FindEmailVerificationTokenAsync(string tokenHash, CancellationToken cancellationToken);
    Task AddPasswordResetTokenAsync(PasswordResetToken token, CancellationToken cancellationToken);
    Task<PasswordResetToken?> FindPasswordResetTokenAsync(string tokenHash, CancellationToken cancellationToken);
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken);
    Task<RefreshToken?> FindRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken);
    Task RevokeRefreshTokensAsync(long userId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
