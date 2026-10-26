using VeganHelper.BLL.DTOs.AI;

namespace VeganHelper.BLL.Contracts;

public interface IGeminiChatClient
{
    Task<GeminiChatCompletion> CompleteAsync(
        string? contextSummary,
        IReadOnlyList<AiChatTurn> turns,
        CancellationToken cancellationToken = default);

    Task<GeminiChatCompletion> CompleteWithPostSearchAsync(
        string? contextSummary,
        IReadOnlyList<AiChatTurn> turns,
        Func<AiPostSearchCriteria, CancellationToken, Task<AiPostSearchResult>> searchPosts,
        CancellationToken cancellationToken = default);
}

public sealed class GeminiChatException(string code, string message, int statusCode, bool retryable = false)
    : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public bool Retryable { get; } = retryable;
}
