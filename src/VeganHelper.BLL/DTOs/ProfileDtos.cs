using System.ComponentModel.DataAnnotations;

namespace VeganHelper.BLL.DTOs;

public sealed record UserProfileDto(
    long Id,
    string Username,
    string Email,
    bool IsEmailVerified,
    bool IsGoogleLinked,
    string? PhoneNumber,
    string DisplayName,
    string? AvatarUrl,
    decimal? HeightCm,
    decimal? WeightKg,
    DateOnly? BirthDate,
    string? BiologicalSex,
    string DietType,
    DateTime CreatedAt);

public sealed record UserSearchResultDto(
    long Id,
    string Username,
    string DisplayName,
    string? AvatarUrl);

public sealed record PublicUserProfileDto(
    long Id,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    string DietType,
    DateTime JoinedAt,
    long PublishedPostCount,
    long ReceivedLikeCount,
    IReadOnlyList<Posts.PostFeedItemDto> Posts);

public sealed class UpdateProfileRequestDto
{
    [StringLength(100, MinimumLength = 3)]
    public string? DisplayName { get; init; }

    public string? PhoneNumber { get; init; }

    [Range(1, 300)]
    public decimal? HeightCm { get; init; }

    [Range(1, 500)]
    public decimal? WeightKg { get; init; }

    public DateOnly? BirthDate { get; init; }

    [RegularExpression("^(male|female|other)$")]
    public string? BiologicalSex { get; init; }

    [RegularExpression("^(vegan|lacto_ovo_vegetarian)$")]
    public string? DietType { get; init; }
}

public sealed record UpdateProfileCommand(
    string? DisplayName,
    string? PhoneNumber,
    decimal? HeightCm,
    decimal? WeightKg,
    DateOnly? BirthDate,
    string? BiologicalSex,
    string? DietType,
    byte[]? AvatarBytes,
    string? AvatarFileName,
    string? AvatarContentType);
