using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public sealed record SummaryVideo(long Id, string Url, int DisplayOrder);
public sealed record SummarySource(long PostId, int Revision, IReadOnlyList<SummaryVideo> Videos);

public interface IPostSummaryRepository
{
    Task<SummarySource?> GetPublicSourceAsync(long postId, CancellationToken ct);
    Task<PostSummary?> GetAsync(long postId, CancellationToken ct);
    Task AddAsync(PostSummary summary, CancellationToken ct);
    Task ResetAsync(long postId, int revision, string mediaIdsJson, CancellationToken ct);
    Task<bool> TryClaimAsync(long postId, int revision, string mediaIdsJson, CancellationToken ct);
    Task<bool> CompleteAsync(long postId, int revision, string mediaIdsJson, string transcript,
        string summary, string model, CancellationToken ct);
    Task FailAsync(long postId, int revision, string mediaIdsJson, string error, CancellationToken ct);
}
