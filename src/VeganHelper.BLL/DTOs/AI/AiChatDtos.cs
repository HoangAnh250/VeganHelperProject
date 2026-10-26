namespace VeganHelper.BLL.DTOs.AI;

public sealed record AiChatRequestDto(
    long? SessionId,
    Guid? GuestSessionId,
    string? Message);

public sealed record AiChatResponseDto(
    long SessionId,
    Guid? GuestSessionId,
    string Answer,
    DateTime CreatedAt,
    string Model)
{
    public int? GuestQuestionsRemaining { get; init; }
    public IReadOnlyList<AiPostCardDto> RecommendedPosts { get; init; } = [];
}

public sealed record AiPostCardDto(long PostId, string Title, string? ThumbnailUrl);
public sealed record AiPostSearchCriteria(
    IReadOnlyList<string>? RequiredIngredients,
    IReadOnlyList<string>? AvailableIngredients,
    IReadOnlyList<string>? ExcludedAllergens,
    string? MealType,
    string? DietType,
    int? MaxMinutes,
    bool StrictOnlyIngredients);
public sealed record AiPostSearchResult(IReadOnlyList<AiPostCardDto> Posts, bool HasMore);

public sealed record AiChatErrorDto(
    string Error,
    string Code,
    bool RequiresRegistration);

public sealed record AiChatMessageDto(
    long Id,
    string SenderType,
    string MessageText,
    DateTime CreatedAt)
{
    public IReadOnlyList<AiPostCardDto> RecommendedPosts { get; init; } = [];
}

public sealed record AiChatHistoryDto(
    long SessionId,
    Guid? GuestSessionId,
    DateTime StartedAt,
    DateTime LastActivityAt,
    IReadOnlyList<AiChatMessageDto> Messages);

public sealed record AiChatTurn(string SenderType, string MessageText);

public sealed record GeminiChatCompletion(
    string Text,
    string Model,
    int? InputTokens,
    int? OutputTokens)
{
    public IReadOnlyList<AiPostCardDto> RecommendedPosts { get; init; } = [];
}
