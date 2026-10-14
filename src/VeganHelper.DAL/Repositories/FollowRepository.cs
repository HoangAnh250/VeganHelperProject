using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class FollowRepository(AppDbContext context) : IFollowRepository
{
    public Task<bool> IsEligibleUserAsync(long userId, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(
            user => user.Id == userId
                && user.IsActive
                && user.DeletedAt == null
                && user.EmailVerifiedAt != null,
            cancellationToken);

    public Task<UserFollow?> FindAsync(
        long followerId,
        long followingId,
        CancellationToken cancellationToken = default) =>
        context.UserFollows.SingleOrDefaultAsync(
            follow => follow.FollowerId == followerId && follow.FollowingId == followingId,
            cancellationToken);

    public Task AddAsync(UserFollow follow, CancellationToken cancellationToken = default) =>
        context.UserFollows.AddAsync(follow, cancellationToken).AsTask();

    public void Remove(UserFollow follow) => context.UserFollows.Remove(follow);

    public Task<long> CountFollowersAsync(long userId, CancellationToken cancellationToken = default) =>
        context.UserFollows
            .Where(follow => follow.FollowingId == userId)
            .Join(
                context.Users.Where(user => user.IsActive && user.DeletedAt == null && user.EmailVerifiedAt != null),
                follow => follow.FollowerId,
                user => user.Id,
                (_, _) => 1)
            .LongCountAsync(cancellationToken);

    public Task<long> CountFollowingAsync(long userId, CancellationToken cancellationToken = default) =>
        context.UserFollows
            .Where(follow => follow.FollowerId == userId)
            .Join(
                context.Users.Where(user => user.IsActive && user.DeletedAt == null && user.EmailVerifiedAt != null),
                follow => follow.FollowingId,
                user => user.Id,
                (_, _) => 1)
            .LongCountAsync(cancellationToken);

    public async Task<(long TotalCount, IReadOnlyList<FollowUserProjection> Items)> GetFollowersAsync(
        long userId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.UserFollows
            .AsNoTracking()
            .Where(follow => follow.FollowingId == userId)
            .Join(
                context.Users.AsNoTracking().Where(user => user.IsActive && user.DeletedAt == null && user.EmailVerifiedAt != null),
                follow => follow.FollowerId,
                user => user.Id,
                (follow, user) => new { follow, user });

        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.follow.CreatedAt)
            .ThenByDescending(item => item.follow.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new FollowUserProjection
            {
                Id = item.user.Id,
                Username = item.user.Username,
                DisplayName = context.UserProfiles
                    .Where(profile => profile.UserId == item.user.Id)
                    .Select(profile => profile.DisplayName)
                    .FirstOrDefault() ?? item.user.Username,
                AvatarUrl = context.UserProfiles
                    .Where(profile => profile.UserId == item.user.Id)
                    .Select(profile => profile.AvatarUrl)
                    .FirstOrDefault(),
                FollowedAt = item.follow.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return (totalCount, items);
    }

    public async Task<(long TotalCount, IReadOnlyList<FollowUserProjection> Items)> GetFollowingAsync(
        long userId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.UserFollows
            .AsNoTracking()
            .Where(follow => follow.FollowerId == userId)
            .Join(
                context.Users.AsNoTracking().Where(user => user.IsActive && user.DeletedAt == null && user.EmailVerifiedAt != null),
                follow => follow.FollowingId,
                user => user.Id,
                (follow, user) => new { follow, user });

        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.follow.CreatedAt)
            .ThenByDescending(item => item.follow.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new FollowUserProjection
            {
                Id = item.user.Id,
                Username = item.user.Username,
                DisplayName = context.UserProfiles
                    .Where(profile => profile.UserId == item.user.Id)
                    .Select(profile => profile.DisplayName)
                    .FirstOrDefault() ?? item.user.Username,
                AvatarUrl = context.UserProfiles
                    .Where(profile => profile.UserId == item.user.Id)
                    .Select(profile => profile.AvatarUrl)
                    .FirstOrDefault(),
                FollowedAt = item.follow.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return (totalCount, items);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
