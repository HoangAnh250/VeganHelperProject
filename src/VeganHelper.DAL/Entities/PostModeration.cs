namespace VeganHelper.DAL.Entities;

public sealed class PostModerationScan
{
    public long Id { get; set; }
    public long PostId { get; set; }
    public Post Post { get; set; } = null!;
    public int Revision { get; set; }
    public string State { get; set; } = "queued";
    public int Attempts { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public string? Model { get; set; }
    public string? PromptVersion { get; set; }
    public double? Confidence { get; set; }
    public string? Summary { get; set; }
    public string FindingsJson { get; set; } = "[]";
    public string? ErrorCode { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? LatencyMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
public sealed class PostModerationDecision
{
    public long Id { get; set; }
    public long PostId { get; set; }
    public int Revision { get; set; }
    public string Action { get; set; } = "";
    public string ActorType { get; set; } = "";
    public long? AdminId { get; set; }
    public long? FlagId { get; set; }
    public long? ScanId { get; set; }
    public string Reason { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
public sealed class PostModerationSettings
{
    public int Id { get; set; } = 1;
    public bool AutoPublishEnabled { get; set; }
    public int Version { get; set; } = 1;
    public long? UpdatedByAdminId { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
