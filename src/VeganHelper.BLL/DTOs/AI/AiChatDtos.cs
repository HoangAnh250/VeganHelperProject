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
    string Model);

public sealed record AiChatMessageDto(
    long Id,
    string SenderType,
    string MessageText,
    DateTime CreatedAt);

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
    int? OutputTokens);
