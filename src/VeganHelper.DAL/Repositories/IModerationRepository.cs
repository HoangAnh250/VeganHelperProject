using Microsoft.EntityFrameworkCore.Storage;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Integrations.Moderation;

namespace VeganHelper.DAL.Repositories;

public sealed class ModerationPostProjection
{
    public long Id { get; init; }
    public long AuthorId { get; init; }
    public string Title { get; init; } = "";
    public string PostType { get; init; } = "";
    public string Status { get; init; } = "";
    public int ContentRevision { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? ScanState { get; init; }
    public double? ConfidenceScore { get; init; }
}
public sealed class ModerationFlagProjection
{
    public long Id { get; init; }
    public long PostId { get; init; }
    public string Title { get; init; } = "";
    public int ContentRevision { get; init; }
    public string Reason { get; init; } = "";
    public string? AiModelName { get; init; }
    public double? ConfidenceScore { get; init; }
    public string FindingsJson { get; init; } = "[]";
    public DateTime CreatedAt { get; init; }
}
public interface IModerationRepository
{
    Task<DatabasePage<ModerationPostProjection>> ListPostsAsync(string? keyword, string? status, int page, int size, CancellationToken ct);
    Task<DatabasePage<ModerationFlagProjection>> ListFlagsAsync(long? postId, double? minConfidence, int page, int size, CancellationToken ct);
    Task<Post?> DetailAsync(long postId, CancellationToken ct);
    Task<Post?> LockPostAsync(long postId, CancellationToken ct);
    Task<Flag?> FindFlagAsync(long flagId, CancellationToken ct);
    Task<List<ModerationFlagProjection>> CurrentFlagsAsync(long postId, CancellationToken ct);
    Task<List<PostModerationDecision>> DecisionsAsync(long postId, CancellationToken ct);
    Task<PostModerationScan?> CurrentScanAsync(long postId, int revision, CancellationToken ct);
    Task ResolveFlagsAsync(long postId, int revision, long adminId, string note, bool keep, DateTime now, CancellationToken ct);
    Task<PostModerationSettings> SettingsAsync(string? lockMode, CancellationToken ct);
    Task ObsoleteScansAsync(long postId, int revision, DateTime now, CancellationToken ct);
    void AddDecision(PostModerationDecision decision);
    void AddFlag(Flag flag);
    void AddUsage(AiUsage usage);
    Task SaveAsync(CancellationToken ct);
    Task<IDbContextTransaction> BeginAsync(CancellationToken ct);
    Task<PostModerationScan?> ClaimAsync(DateTime now, Guid lease, CancellationToken ct);
    Task<PostModerationScan?> LockScanAsync(long scanId, CancellationToken ct);
    Task<ModerationAiInput?> InputAsync(long postId, int revision, CancellationToken ct);
}
