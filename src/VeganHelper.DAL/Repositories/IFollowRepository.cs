using VeganHelper.DAL.Entities;
using Microsoft.EntityFrameworkCore.Storage;

namespace VeganHelper.DAL.Repositories;

public interface IFollowRepository
{
    Task<IDbContextTransaction> BeginTransactionAsync(long followerId, long followingId, CancellationToken cancellationToken = default);
    Task CancelNewPostDeliveriesAsync(long followId, CancellationToken cancellationToken = default);
    Task<bool> IsEligibleUserAsync(long userId, CancellationToken cancellationToken = default);
    Task<UserFollow?> FindAsync(long followerId, long followingId, CancellationToken cancellationToken = default);
    Task AddAsync(UserFollow follow, CancellationToken cancellationToken = default);
    void Remove(UserFollow follow);
    Task<long> CountFollowersAsync(long userId, CancellationToken cancellationToken = default);
    Task<long> CountFollowingAsync(long userId, CancellationToken cancellationToken = default);
    Task<(long TotalCount, IReadOnlyList<FollowUserProjection> Items)> GetFollowersAsync(
        long userId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<(long TotalCount, IReadOnlyList<FollowUserProjection> Items)> GetFollowingAsync(
        long userId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
