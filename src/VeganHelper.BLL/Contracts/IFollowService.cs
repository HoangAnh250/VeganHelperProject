using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Follows;

namespace VeganHelper.BLL.Contracts;

public interface IFollowService
{
    Task<FollowStatusDto> SetNewPostNotificationsAsync(long followerId, long followingId, bool enabled, CancellationToken cancellationToken = default);
    Task<FollowStatusDto> FollowAsync(
        long followerId,
        long followingId,
        CancellationToken cancellationToken = default);

    Task<FollowStatusDto> UnfollowAsync(
        long followerId,
        long followingId,
        CancellationToken cancellationToken = default);

    Task<FollowStatusDto> GetStatusAsync(
        long followerId,
        long followingId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<FollowUserDto>> GetFollowersAsync(
        long userId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResult<FollowUserDto>> GetFollowingAsync(
        long userId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
}
