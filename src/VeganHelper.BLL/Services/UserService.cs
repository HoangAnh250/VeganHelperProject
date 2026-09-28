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

        return ServiceResult<UserProfileDto>.Ok(Map(user, profile));
    }

    public async Task<ServiceResult<UserProfileDto>> UpdateProfileAsync(long userId, UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        var user = await repository.FindUserAsync(userId, cancellationToken);
        if (user is null || user.DeletedAt is not null)
            return ServiceResult<UserProfileDto>.Fail("User was not found.", 404);

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
        return ServiceResult<UserProfileDto>.Ok(Map(user, profile));
    }

    private static UserProfileDto Map(User user, UserProfile profile) => new(
        user.Id,
        user.Username,
        user.Email,
        user.EmailVerifiedAt is not null,
        user.PhoneNumber,
        profile.DisplayName,
        profile.AvatarUrl,
        profile.HeightCm,
        profile.WeightKg,
        profile.BirthDate,
        profile.BiologicalSex,
        profile.DietType);

}
