using Microsoft.EntityFrameworkCore;
using Npgsql;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class AuthRepository(AppDbContext db) : IAuthRepository
{
    public Task<bool> HasActiveBanAsync(long userId, DateTime now, CancellationToken ct) =>
        db.UserBans.AnyAsync(b => b.UserId == userId && b.UnbannedAt == null && (b.ExpiresAt == null || b.ExpiresAt > now), ct);

    public Task<string?> FindSessionRoleAsync(long userId, int tokenVersion, DateTime now, CancellationToken ct) =>
        (from user in db.Users.AsNoTracking()
         join role in db.Roles on user.RoleId equals role.Id
         where user.Id == userId && user.TokenVersion == tokenVersion && user.IsActive && user.DeletedAt == null
            && !db.UserBans.Any(b => b.UserId == user.Id && b.UnbannedAt == null && (b.ExpiresAt == null || b.ExpiresAt > now))
         select role.RoleName).SingleOrDefaultAsync(ct);
    public Task<User?> FindUserByEmailAsync(string email, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);

    public Task<User?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(x => x.Username == username, cancellationToken);

    public async Task<User?> FindUserByIdentifierAsync(string identifier, CancellationToken cancellationToken)
    {
        if (identifier.Contains('@'))
        {
            var owner = await FindUserByEmailAsync(identifier, cancellationToken);
            if (owner is not null) return owner;
        }
        return await FindUserByUsernameAsync(identifier, cancellationToken);
    }

    public Task<User?> FindUserByIdAsync(long id, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<UserIdentity?> FindIdentityAsync(string provider, string providerSubject, CancellationToken cancellationToken) =>
        db.UserIdentities.SingleOrDefaultAsync(
            x => x.Provider == provider && x.ProviderSubject == providerSubject,
            cancellationToken);

    public Task<UserIdentity?> FindIdentityByUserAsync(long userId, string provider, CancellationToken cancellationToken) =>
        db.UserIdentities.SingleOrDefaultAsync(
            x => x.UserId == userId && x.Provider == provider,
            cancellationToken);

    public Task<string?> FindRoleNameAsync(int roleId, CancellationToken cancellationToken) =>
        db.Roles.Where(x => x.Id == roleId).Select(x => x.RoleName).SingleOrDefaultAsync(cancellationToken);

    public Task AddUserAsync(User user, CancellationToken cancellationToken) =>
        db.Users.AddAsync(user, cancellationToken).AsTask();

    public Task AddUserIdentityAsync(UserIdentity identity, CancellationToken cancellationToken) =>
        db.UserIdentities.AddAsync(identity, cancellationToken).AsTask();

    public void RemoveUserIdentity(UserIdentity identity) => db.UserIdentities.Remove(identity);

    public Task AddEmailVerificationTokenAsync(EmailVerificationToken token, CancellationToken cancellationToken) =>
        db.EmailVerificationTokens.AddAsync(token, cancellationToken).AsTask();

    public Task RevokeEmailVerificationTokensAsync(long userId, string purpose, DateTime revokedAt, CancellationToken cancellationToken) =>
        db.EmailVerificationTokens
            .Where(x => x.UserId == userId && x.Purpose == purpose && x.ConsumedAt == null && x.ExpiresAt > revokedAt)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.ConsumedAt, revokedAt),
                cancellationToken);

    public async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> LockOtpUserAsync(User user, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM users WHERE id = {user.Id} FOR UPDATE", ct);
            await db.Entry(user).ReloadAsync(ct);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public Task<List<EmailVerificationToken>> GetRecentOtpIssuancesAsync(long userId, DateTime since, int limit, CancellationToken ct) =>
        db.EmailVerificationTokens.AsNoTracking().Where(x => x.UserId == userId && x.CreatedAt > since)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(limit).ToListAsync(ct);

    public Task<EmailVerificationToken?> FindLatestOtpAsync(long userId, string purpose, CancellationToken ct) =>
        db.EmailVerificationTokens.Where(x => x.UserId == userId && x.Purpose == purpose)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);

    public async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> LockRegistrationAsync(string email, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({email}, 39117))", ct);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public Task<List<RegistrationChallenge>> GetRegistrationChallengesAsync(string email, DateTime since, int limit, CancellationToken ct) =>
        db.Set<RegistrationChallenge>().Where(x => x.Email == email && x.CreatedAt > since)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(limit).ToListAsync(ct);

    public async Task<bool> UpdatePendingUsernameAsync(User user, string username, CancellationToken ct)
    {
        // Username is an EF alternate key. Update it in the database and detach the old key.
        var changed = await db.Users.Where(x => x.Id == user.Id && !x.IsActive &&
                x.EmailVerifiedAt == null && x.DeletedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Username, username), ct);
        db.Entry(user).State = EntityState.Detached;
        return changed == 1;
    }

    public async Task<bool> CompleteEmailChangeAsync(long userId, string email, DateTime now, CancellationToken ct)
    {
        try
        {
            // Email is an EF alternate key. Token consumption and session revocation use the caller's transaction.
            var changed = await db.Users.Where(x => x.Id == userId && x.PendingEmail == email)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Email, email)
                    .SetProperty(x => x.PendingEmail, (string?)null)
                    .SetProperty(x => x.EmailVerifiedAt, now)
                    .SetProperty(x => x.IsActive, true)
                    .SetProperty(x => x.UpdatedAt, now)
                    .SetProperty(x => x.TokenVersion, x => x.TokenVersion + 1), ct);
            return changed == 1;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation && ex.ConstraintName == "UQ_users_email")
        {
            return false;
        }
    }

    public Task AddRegistrationChallengeAsync(RegistrationChallenge challenge, CancellationToken ct) =>
        db.Set<RegistrationChallenge>().AddAsync(challenge, ct).AsTask();

    public Task<string?> FindRegistrationEmailAsync(string proofHash, CancellationToken ct) =>
        db.Set<RegistrationChallenge>().Where(x => x.ProofHash == proofHash && x.CompletedAt == null)
            .Select(x => x.Email).SingleOrDefaultAsync(ct);

    public Task<List<DateTime>> GetRecentPasswordResetRequestsAsync(long userId, DateTime since, CancellationToken ct) =>
        db.PasswordResetTokens.Where(x => x.UserId == userId && x.CreatedAt > since)
            .OrderByDescending(x => x.CreatedAt).Select(x => x.CreatedAt).Take(5).ToListAsync(ct);

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
