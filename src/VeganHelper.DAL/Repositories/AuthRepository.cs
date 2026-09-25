using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class AuthRepository(AppDbContext db) : IAuthRepository
{
    public Task<User?> FindUserByEmailAsync(string email, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);

    public Task<User?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(x => x.Username == username, cancellationToken);

    public Task<User?> FindUserByIdAsync(long id, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<string?> FindRoleNameAsync(int roleId, CancellationToken cancellationToken) =>
        db.Roles.Where(x => x.Id == roleId).Select(x => x.RoleName).SingleOrDefaultAsync(cancellationToken);

    public Task AddUserAsync(User user, CancellationToken cancellationToken) =>
        db.Users.AddAsync(user, cancellationToken).AsTask();

    public Task AddEmailVerificationTokenAsync(EmailVerificationToken token, CancellationToken cancellationToken) =>
        db.EmailVerificationTokens.AddAsync(token, cancellationToken).AsTask();

    public Task<EmailVerificationToken?> FindEmailVerificationTokenAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.EmailVerificationTokens.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public Task AddPasswordResetTokenAsync(PasswordResetToken token, CancellationToken cancellationToken) =>
        db.PasswordResetTokens.AddAsync(token, cancellationToken).AsTask();

    public Task<PasswordResetToken?> FindPasswordResetTokenAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.PasswordResetTokens.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken) =>
        db.RefreshTokens.AddAsync(token, cancellationToken).AsTask();

    public Task<RefreshToken?> FindRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public async Task RevokeRefreshTokensAsync(long userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await db.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAt, now), cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
