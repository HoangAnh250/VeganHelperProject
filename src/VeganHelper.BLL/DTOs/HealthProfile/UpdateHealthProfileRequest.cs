namespace VeganHelper.BLL.DTOs.HealthProfile;

public class UpdateHealthProfileRequest
{
    public decimal HeightCm { get; set; }
    public decimal WeightKg { get; set; }
    public string BiologicalSex { get; set; } = string.Empty;
    public DateOnly BirthDate { get; set; }
    public string DietType { get; set; } = string.Empty;
    public string ActivityLevel { get; set; } = string.Empty;
}
