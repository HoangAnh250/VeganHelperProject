namespace VeganHelper.BLL.Contracts;

public sealed record PostSummaryDto(long PostId, string Status, string? Transcript,
    string? Summary, string? Error, DateTime? RequestedAt, DateTime? GeneratedAt);

public interface IPostSummaryService
{
    Task<PostSummaryDto?> GetAsync(long postId, CancellationToken ct);
    Task<PostSummaryDto?> RequestAsync(long postId, CancellationToken ct);
    Task ProcessAsync(long postId, CancellationToken ct);
}

public interface IVideoSummaryAiClient
{
    Task<string> TranscribeAsync(string mediaUrl, CancellationToken ct);
    Task<string> SummarizeAsync(string transcript, CancellationToken ct);
    string SummaryModel { get; }
}

public interface IPostSummaryQueue
{
    void Enqueue(long postId);
}
