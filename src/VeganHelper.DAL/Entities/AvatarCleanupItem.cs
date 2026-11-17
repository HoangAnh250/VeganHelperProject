namespace VeganHelper.DAL.Entities;

public sealed class AvatarCleanupItem
{
    public long Id { get; set; }
    public string Url { get; set; } = "";
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }
}
