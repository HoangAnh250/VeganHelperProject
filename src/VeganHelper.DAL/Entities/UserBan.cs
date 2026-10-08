namespace VeganHelper.DAL.Entities;

public sealed class UserBan
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long BannedByAdminId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public long? UnbannedByAdminId { get; set; }
    public string? UnbanReason { get; set; }
    public DateTime? UnbannedAt { get; set; }
}
