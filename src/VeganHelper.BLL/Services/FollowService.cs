using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Follows;
using VeganHelper.BLL.Exceptions;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class FollowService(IFollowRepository repository) : IFollowService
{
    private const int MaxPageSize = 50;

    public async Task<FollowStatusDto> FollowAsync(
        long followerId,
        long followingId,
        CancellationToken cancellationToken = default)
    {
        await EnsureEligibleActorAsync(followerId, cancellationToken);
        await EnsureEligibleTargetAsync(followingId, cancellationToken);
        EnsureNotSelf(followerId, followingId);

        var existing = await repository.FindAsync(followerId, followingId, cancellationToken);
        if (existing is not null)
            throw new ConflictException("You are already following this user.");

        await repository.AddAsync(new UserFollow
        {
            FollowerId = followerId,
            FollowingId = followingId,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return await BuildStatusAsync(followerId, followingId, cancellationToken);
    }

    public async Task<FollowStatusDto> UnfollowAsync(
        long followerId,
        long followingId,
        CancellationToken cancellationToken = default)
    {
        await EnsureEligibleActorAsync(followerId, cancellationToken);
        await EnsureEligibleTargetAsync(followingId, cancellationToken);
        EnsureNotSelf(followerId, followingId);

        var existing = await repository.FindAsync(followerId, followingId, cancellationToken);
        if (existing is null)
            throw new NotFoundException("Follow relationship was not found.");

        repository.Remove(existing);
        await repository.SaveChangesAsync(cancellationToken);

        return await BuildStatusAsync(followerId, followingId, cancellationToken);
    }

    public async Task<FollowStatusDto> GetStatusAsync(
        long followerId,
        long followingId,
        CancellationToken cancellationToken = default)
    {
        await EnsureEligibleActorAsync(followerId, cancellationToken);
        await EnsureEligibleTargetAsync(followingId, cancellationToken);
        EnsureNotSelf(followerId, followingId);

        return await BuildStatusAsync(followerId, followingId, cancellationToken);
    }

    public async Task<PagedResult<FollowUserDto>> GetFollowersAsync(
        long userId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await EnsureEligibleTargetAsync(userId, cancellationToken);
        (pageIndex, pageSize) = NormalizePage(pageIndex, pageSize);

        var (totalCount, projections) = await repository.GetFollowersAsync(
            userId,
            pageIndex,
            pageSize,
            cancellationToken);

        return ToPagedResult(totalCount, pageIndex, pageSize, projections);
    }

    public async Task<PagedResult<FollowUserDto>> GetFollowingAsync(
        long userId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await EnsureEligibleTargetAsync(userId, cancellationToken);
        (pageIndex, pageSize) = NormalizePage(pageIndex, pageSize);

        var (totalCount, projections) = await repository.GetFollowingAsync(
            userId,
            pageIndex,
            pageSize,
            cancellationToken);

        return ToPagedResult(totalCount, pageIndex, pageSize, projections);
    }

    private async Task<FollowStatusDto> BuildStatusAsync(
        long followerId,
        long followingId,
        CancellationToken cancellationToken)
    {
        var existing = await repository.FindAsync(followerId, followingId, cancellationToken);
        var followersCount = await repository.CountFollowersAsync(followingId, cancellationToken);
        var followingCount = await repository.CountFollowingAsync(followingId, cancellationToken);

        return new FollowStatusDto(
            followingId,
            existing is not null,
            followersCount,
            followingCount);
    }

    private async Task EnsureEligibleActorAsync(long userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
            throw new UnauthorizedAccessException("A valid authenticated user is required.");

        if (!await repository.IsEligibleUserAsync(userId, cancellationToken))
            throw new UnauthorizedAccessException("Your account is not active and verified.");
    }

    private async Task EnsureEligibleTargetAsync(long userId, CancellationToken cancellationToken)
    {
        if (userId <= 0 || !await repository.IsEligibleUserAsync(userId, cancellationToken))
            throw new NotFoundException("The target user was not found or is not available.");
    }

    private static void EnsureNotSelf(long followerId, long followingId)
    {
        if (followerId == followingId)
            throw new ArgumentException("You cannot follow yourself.");
    }

    private static (int PageIndex, int PageSize) NormalizePage(int pageIndex, int pageSize) =>
        (pageIndex < 1 ? 1 : pageIndex, pageSize < 1 ? 10 : Math.Min(pageSize, MaxPageSize));

    private static PagedResult<FollowUserDto> ToPagedResult(
        long totalCount,
        int pageIndex,
        int pageSize,
        IReadOnlyList<FollowUserProjection> projections) =>
        new()
        {
            TotalItems = totalCount,
            TotalCount = totalCount > int.MaxValue ? int.MaxValue : (int)totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = projections.Select(user => new FollowUserDto(
                user.Id,
                user.Username,
                user.DisplayName,
                user.AvatarUrl,
                user.FollowedAt)).ToList()
        };
}
