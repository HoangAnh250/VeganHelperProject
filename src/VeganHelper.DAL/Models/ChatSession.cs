namespace VeganHelper.DAL.Models;

public sealed class ChatSession
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public Guid? GuestSessionId { get; set; }
    public string? ContextSummary { get; set; }
    public long? SummaryThroughMessageId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
}
