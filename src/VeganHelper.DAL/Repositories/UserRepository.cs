using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public async Task<(User User, UserProfile Profile)?> FindProfileAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null) return null;
        var profile = await db.UserProfiles.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (profile is null) return null;
        return (user, profile);
    }

    public Task<User?> FindUserAsync(long userId, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

    public Task<UserProfile?> FindUserProfileAsync(long userId, CancellationToken cancellationToken) =>
        db.UserProfiles.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public Task AddUserProfileAsync(UserProfile profile, CancellationToken cancellationToken) =>
        db.UserProfiles.AddAsync(profile, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
