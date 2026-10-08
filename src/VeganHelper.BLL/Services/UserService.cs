using System.Text.RegularExpressions;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;
using VeganHelper.DAL.Storage;

namespace VeganHelper.BLL.Services;

public sealed class UserService(IUserRepository repository, IAvatarStorage avatarStorage) : IUserService
{
    private static readonly Regex VietnamesePhone = new("^(0|\\+84)(3|5|7|8|9)[0-9]{8}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private const long MaxAvatarBytes = 5 * 1024 * 1024;

    public async Task<ServiceResult<UserProfileDto>> GetProfileAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await repository.FindUserAsync(userId, cancellationToken);
        if (user is null || user.DeletedAt is not null)
            return ServiceResult<UserProfileDto>.Fail("User profile was not found.", 404);

        var profile = await repository.FindUserProfileAsync(userId, cancellationToken);
        if (profile is null)
        {
            profile = new UserProfile
            {
                UserId = userId,
                DisplayName = user.Username,
                DietType = "vegan"
            };
            await repository.AddUserProfileAsync(profile, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
        }

        var isGoogleLinked = await repository.IsGoogleLinkedAsync(userId, cancellationToken);
        return ServiceResult<UserProfileDto>.Ok(Map(user, profile, isGoogleLinked));
    }

    public async Task<ServiceResult<UserProfileDto>> UpdateProfileAsync(long userId, UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        var user = await repository.FindUserAsync(userId, cancellationToken);
        if (user is null || user.DeletedAt is not null)
            return ServiceResult<UserProfileDto>.Fail("User was not found.", 404);

        if (command.DisplayName is not null && command.DisplayName.Trim().Length < 3)
            return ServiceResult<UserProfileDto>.Fail("Full name must contain at least 3 characters.", 400);
        if (command.BirthDate is { } birthDate && birthDate >= DateOnly.FromDateTime(DateTime.UtcNow))
            return ServiceResult<UserProfileDto>.Fail("Date of birth must be a valid date before today.", 400);
        if (command.HeightCm is <= 0 or > 300 || command.WeightKg is <= 0 or > 500)
            return ServiceResult<UserProfileDto>.Fail("Height and weight must be positive and within the allowed range.", 400);
        if (command.BiologicalSex is not null && command.BiologicalSex is not ("male" or "female" or "other"))
            return ServiceResult<UserProfileDto>.Fail("Biological sex is invalid.", 400);
        if (command.DietType is not null && command.DietType is not ("vegan" or "lacto_ovo_vegetarian"))
            return ServiceResult<UserProfileDto>.Fail("Diet type is invalid.", 400);

        if (command.PhoneNumber is not null)
        {
            var phone = command.PhoneNumber.Trim();
            if (phone.Length > 0 && !VietnamesePhone.IsMatch(phone))
                return ServiceResult<UserProfileDto>.Fail("Phone number must be a valid Vietnamese number.", 400);
            user.PhoneNumber = phone.Length == 0 ? null : phone;
        }

        if (command.AvatarBytes is not null)
        {
            if (command.AvatarBytes.LongLength > MaxAvatarBytes)
                return ServiceResult<UserProfileDto>.Fail("Avatar must not exceed 5 MB.", 400);
            if (command.AvatarContentType is not ("image/jpeg" or "image/png"))
                return ServiceResult<UserProfileDto>.Fail("Avatar must be a JPG or PNG image.", 400);
            if (string.IsNullOrWhiteSpace(command.AvatarFileName))
                return ServiceResult<UserProfileDto>.Fail("Avatar file name is required.", 400);
        }

        var profile = await repository.FindUserProfileAsync(userId, cancellationToken);
        if (profile is null)
        {
            profile = new UserProfile
            {
                UserId = userId,
                DisplayName = user.Username,
                DietType = "vegan"
            };
            await repository.AddUserProfileAsync(profile, cancellationToken);
        }

        if (command.DisplayName is not null && !string.IsNullOrWhiteSpace(command.DisplayName))
            profile.DisplayName = command.DisplayName.Trim();
        if (command.HeightCm.HasValue) profile.HeightCm = command.HeightCm.Value;
        if (command.WeightKg.HasValue) profile.WeightKg = command.WeightKg.Value;
        if (command.BirthDate.HasValue) profile.BirthDate = command.BirthDate.Value;
        if (command.BiologicalSex is not null) profile.BiologicalSex = command.BiologicalSex;
        if (command.DietType is not null) profile.DietType = command.DietType;
        if (command.AvatarBytes is not null)
        {
            profile.AvatarUrl = await avatarStorage.SaveAsync(
                command.AvatarBytes,
                command.AvatarFileName ?? "avatar.jpg",
                command.AvatarContentType!,
                cancellationToken);
        }
        profile.UpdatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        var isGoogleLinked = await repository.IsGoogleLinkedAsync(userId, cancellationToken);
        return ServiceResult<UserProfileDto>.Ok(Map(user, profile, isGoogleLinked));
    }

    public async Task<ServiceResult<PagedResult<UserSearchResultDto>>> SearchUsersAsync(
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedKeyword = keyword?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedKeyword) || normalizedKeyword.Length < 2)
        {
            return ServiceResult<PagedResult<UserSearchResultDto>>.Fail(
                "Keyword must contain at least 2 characters.",
                400);
        }

        pageIndex = pageIndex < 1 ? 1 : pageIndex;
        pageSize = pageSize < 1 ? 10 : Math.Min(pageSize, 50);

        var (totalCount, projections) = await repository.SearchUsersAsync(
            normalizedKeyword,
            pageIndex,
            pageSize,
            cancellationToken);

        var result = new PagedResult<UserSearchResultDto>
        {
            TotalItems = totalCount,
            TotalCount = totalCount > int.MaxValue ? int.MaxValue : (int)totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = projections.Select(user => new UserSearchResultDto(
                user.Id,
                user.Username,
                user.DisplayName,
                user.AvatarUrl))
        };

        return ServiceResult<PagedResult<UserSearchResultDto>>.Ok(result);
    }

    public async Task<ServiceResult<PublicUserProfileDto>> GetPublicProfileAsync(
        long userId,
        CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return ServiceResult<PublicUserProfileDto>.Fail("User profile was not found.", 404);
        }

        var projection = await repository.GetPublicProfileAsync(userId, cancellationToken);
        if (projection is null)
        {
            return ServiceResult<PublicUserProfileDto>.Fail("User profile was not found.", 404);
        }

        var posts = projection.Posts.Select(post => new VeganHelper.BLL.DTOs.Posts.PostFeedItemDto
        {
            Id = post.Id,
            Title = post.Title,
            PostType = post.PostType,
            ThumbnailUrl = post.ThumbnailUrl,
            AuthorName = post.AuthorName,
            AvatarUrl = post.AvatarUrl,
            ViewCount = post.ViewCount,
            CreatedAt = post.CreatedAt
        }).ToList();

        var result = new PublicUserProfileDto(
            projection.Id,
            projection.Username,
            projection.DisplayName,
            projection.AvatarUrl,
            projection.DietType,
            projection.JoinedAt,
            projection.PublishedPostCount,
            projection.ReceivedLikeCount,
            posts);

        return ServiceResult<PublicUserProfileDto>.Ok(result);
    }

    private static UserProfileDto Map(User user, UserProfile profile, bool isGoogleLinked) => new(
        user.Id,
        user.Username,
        user.Email,
        user.EmailVerifiedAt is not null,
        isGoogleLinked,
        user.PhoneNumber,
        profile.DisplayName,
        profile.AvatarUrl,
        profile.HeightCm,
        profile.WeightKg,
        profile.BirthDate,
        profile.BiologicalSex,
        profile.DietType,
        user.CreatedAt);

}
