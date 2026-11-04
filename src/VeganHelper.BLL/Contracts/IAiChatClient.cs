using VeganHelper.BLL.DTOs.AI;

namespace VeganHelper.BLL.Contracts;

public interface IAiChatClient
{
    Task<AiChatCompletion> SummarizeConversationAsync(
        string? previousSummary,
        IReadOnlyList<AiChatTurn> turnsToSummarize,
        CancellationToken cancellationToken = default);

    Task<AiChatCompletion> CompleteAsync(
        string? contextSummary,
        IReadOnlyList<AiChatTurn> turns,
        CancellationToken cancellationToken = default);

    Task<AiChatCompletion> CompleteWithPostSearchAsync(
        string? contextSummary,
        IReadOnlyList<AiChatTurn> turns,
        Func<AiPostSearchCriteria, CancellationToken, Task<AiPostSearchResult>> searchPosts,
        CancellationToken cancellationToken = default);
}

public sealed class AiChatProviderException(string code, string message, int statusCode, bool retryable = false)
    : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public bool Retryable { get; } = retryable;
}
