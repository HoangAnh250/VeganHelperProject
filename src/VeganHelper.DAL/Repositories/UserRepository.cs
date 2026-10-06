using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    private const string GoogleProvider = "google";

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

    public Task<bool> IsGoogleLinkedAsync(long userId, CancellationToken cancellationToken) =>
        db.UserIdentities.AnyAsync(
            x => x.UserId == userId && x.Provider == GoogleProvider,
            cancellationToken);

    public Task AddUserProfileAsync(UserProfile profile, CancellationToken cancellationToken) =>
        db.UserProfiles.AddAsync(profile, cancellationToken).AsTask();

    public async Task<(long TotalCount, IReadOnlyList<UserSearchProjection> Items)> SearchUsersAsync(
        string keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedKeyword = keyword.ToLowerInvariant();
        var query = db.Users
            .AsNoTracking()
            .Where(user => user.IsActive
                && user.DeletedAt == null
                && (EF.Functions.Collate(user.Username.ToLower(), "C").Contains(normalizedKeyword)
                    || db.UserProfiles.Any(profile =>
                        profile.UserId == user.Id && profile.DisplayName.ToLower().Contains(normalizedKeyword))))
            .Select(user => new
            {
                user.Id,
                user.Username,
                DisplayName = db.UserProfiles
                    .Where(profile => profile.UserId == user.Id)
                    .Select(profile => profile.DisplayName)
                    .FirstOrDefault(),
                AvatarUrl = db.UserProfiles
                    .Where(profile => profile.UserId == user.Id)
                    .Select(profile => profile.AvatarUrl)
                    .FirstOrDefault()
            });

        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(user => EF.Functions.Collate(user.Username.ToLower(), "C").StartsWith(normalizedKeyword))
            .ThenByDescending(user => user.DisplayName != null && user.DisplayName.ToLower().StartsWith(normalizedKeyword))
            .ThenBy(user => user.Username)
            .ThenBy(user => user.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new UserSearchProjection
            {
                Id = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName ?? user.Username,
                AvatarUrl = user.AvatarUrl
            })
            .ToListAsync(cancellationToken);

        return (totalCount, items);
    }

    public async Task<PublicUserProfileProjection?> GetPublicProfileAsync(
        long userId,
        CancellationToken cancellationToken)
    {
        var user = await db.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId
                && candidate.IsActive
                && candidate.DeletedAt == null)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Username,
                candidate.CreatedAt,
                DisplayName = db.UserProfiles
                    .Where(profile => profile.UserId == candidate.Id)
                    .Select(profile => profile.DisplayName)
                    .FirstOrDefault(),
                AvatarUrl = db.UserProfiles
                    .Where(profile => profile.UserId == candidate.Id)
                    .Select(profile => profile.AvatarUrl)
                    .FirstOrDefault(),
                DietType = db.UserProfiles
                    .Where(profile => profile.UserId == candidate.Id)
                    .Select(profile => profile.DietType)
                    .FirstOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        var publishedPostsQuery = db.Posts
            .AsNoTracking()
            .Where(post => post.AuthorId == userId
                && post.Status == "published"
                && !post.IsDeleted);

        var posts = await publishedPostsQuery
            .OrderByDescending(post => post.CreatedAt)
            .ThenByDescending(post => post.Id)
            .Select(post => new PostFeedProjection
            {
                Id = post.Id,
                Title = post.Title,
                PostType = post.PostType,
                ThumbnailUrl = post.Media
                    .Where(media => media.IsPrimary)
                    .Select(media => media.MediaUrl)
                    .FirstOrDefault(),
                AuthorName = user.DisplayName ?? user.Username,
                AvatarUrl = user.AvatarUrl,
                ViewCount = post.ViewCount,
                CreatedAt = post.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var publishedPostCount = posts.LongCount();
        var receivedLikeCount = await db.PostLikes
            .LongCountAsync(like => publishedPostsQuery.Any(post => post.Id == like.PostId), cancellationToken);

        return new PublicUserProfileProjection
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName ?? user.Username,
            AvatarUrl = user.AvatarUrl,
            DietType = user.DietType ?? "vegan",
            JoinedAt = user.CreatedAt,
            PublishedPostCount = publishedPostCount,
            ReceivedLikeCount = receivedLikeCount,
            Posts = posts
        };
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
