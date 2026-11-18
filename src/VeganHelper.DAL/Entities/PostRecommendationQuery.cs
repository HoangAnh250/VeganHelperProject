namespace VeganHelper.DAL.Entities;

public sealed record PostRecommendationQuery(
    IReadOnlyList<string> RequiredIngredients,
    IReadOnlyList<string> AvailableIngredients,
    IReadOnlyList<string> Allergies,
    IReadOnlyList<long> ExcludedPostIds,
    string? MealType,
    string? DietType,
    int? MaxMinutes,
    bool StrictOnlyIngredients,
    IReadOnlyList<long>? AllergyIngredientIds = null)
{
    public IReadOnlyList<string> SearchTerms { get; init; } = [];
    public IReadOnlyList<long>? CandidatePostIds { get; init; }
}

public sealed record PostRecommendationProjection(long PostId, string Title, string? ThumbnailUrl);
