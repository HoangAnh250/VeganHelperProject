namespace VeganHelper.DAL.Entities;

public sealed class RegistrationChallenge
{
    public long Id { get; set; }
    public string Email { get; set; } = "";
    public string OtpHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int FailedAttempts { get; set; }
    public string? ProofHash { get; set; }
    public DateTime? ProofExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? PendingUsername { get; set; }
    public string? PendingDisplayName { get; set; }
    public string? PendingPasswordHash { get; set; }
}
