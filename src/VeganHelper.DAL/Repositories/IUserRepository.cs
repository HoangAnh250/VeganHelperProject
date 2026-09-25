using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public interface IUserRepository
{
    Task<(User User, UserProfile Profile)?> FindProfileAsync(long userId, CancellationToken cancellationToken);
    Task<User?> FindUserAsync(long userId, CancellationToken cancellationToken);
    Task<UserProfile?> FindUserProfileAsync(long userId, CancellationToken cancellationToken);
    Task AddUserProfileAsync(UserProfile profile, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
