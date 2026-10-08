namespace VeganHelper.DAL.Entities;

public sealed class AdminAuditLog
{
    public long Id { get; set; }
    public long AdminId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string? TargetId { get; set; }
    public string? IpAddress { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
