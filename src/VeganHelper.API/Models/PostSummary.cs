namespace VeganHelper.API.Models;

public sealed class PostSummary
{
    public long PostId { get; set; }
    public long SourceMediaId { get; set; }
    public string? Transcript { get; set; }
    public string? AiGeneratedText { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ModelName { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? GeneratedAt { get; set; }
}
