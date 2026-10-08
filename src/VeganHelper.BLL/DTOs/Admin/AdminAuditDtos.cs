using System.ComponentModel.DataAnnotations;

namespace VeganHelper.BLL.DTOs.Admin;

// Populated by the API from its authenticated principal and connection, never from a request body.
public sealed record AdminActor(long UserId, string? IpAddress, string TraceId);

public sealed class AdminAuditListRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    [Range(1, long.MaxValue)] public long? AdminId { get; set; }
    [MaxLength(100)] public string? Action { get; set; }
    [MaxLength(50)] public string? TargetType { get; set; }
    [MaxLength(100)] public string? TargetId { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}

public sealed class AdminAuditLogDto
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
