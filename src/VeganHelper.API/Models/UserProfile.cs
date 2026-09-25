namespace VeganHelper.API.Models;

public sealed class UserProfile
{
    public long UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WeightKg { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? BiologicalSex { get; set; }
    public string DietType { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
}
