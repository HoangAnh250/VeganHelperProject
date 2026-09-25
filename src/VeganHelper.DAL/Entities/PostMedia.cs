namespace VeganHelper.DAL.Entities;

public sealed class PostMedia
{
    public long Id { get; set; }
    public long PostId { get; set; }
    public string MediaUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string? CloudPublicId { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int? DurationSeconds { get; set; }
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
    public string ProcessingStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
