namespace VeganHelper.DAL.Entities;

public sealed class Flag
{
    public long Id { get; set; }
    public long? ReporterId { get; set; }
    public long? PostId { get; set; }
    public long? CommentId { get; set; }
    public long? ShopId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long? ResolvedByAdminId { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string? AiModelName { get; set; }
}
