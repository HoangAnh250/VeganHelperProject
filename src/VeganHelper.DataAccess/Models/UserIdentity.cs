namespace VeganHelper.DataAccess.Models;

public sealed class UserIdentity
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderSubject { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
