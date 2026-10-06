using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public interface IUserRepository
{
    Task<(User User, UserProfile Profile)?> FindProfileAsync(long userId, CancellationToken cancellationToken);
    Task<User?> FindUserAsync(long userId, CancellationToken cancellationToken);
    Task<UserProfile?> FindUserProfileAsync(long userId, CancellationToken cancellationToken);
    Task<bool> IsGoogleLinkedAsync(long userId, CancellationToken cancellationToken);
    Task AddUserProfileAsync(UserProfile profile, CancellationToken cancellationToken);
    Task<(long TotalCount, IReadOnlyList<UserSearchProjection> Items)> SearchUsersAsync(
        string keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);
    Task<PublicUserProfileProjection?> GetPublicProfileAsync(
        long userId,
        CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
