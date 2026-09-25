namespace VeganHelper.DAL.Entities;

public sealed class AiUsage
{
    public long Id { get; set; }
    public Guid RequestId { get; set; }
    public long? UserId { get; set; }
    public Guid? GuestSessionId { get; set; }
    public string FeatureType { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public DateTime CreatedAt { get; set; }
}
