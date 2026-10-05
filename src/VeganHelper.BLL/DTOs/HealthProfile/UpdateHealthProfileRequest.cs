using System.ComponentModel.DataAnnotations;

namespace VeganHelper.BLL.DTOs.HealthProfile;

public class UpdateHealthProfileRequest
{
    [Range(typeof(decimal), "100", "250")]
    public decimal HeightCm { get; set; }
    [Range(typeof(decimal), "30", "200")]
    public decimal WeightKg { get; set; }
    [Required, MaxLength(20)]
    public string BiologicalSex { get; set; } = string.Empty;
    public DateOnly BirthDate { get; set; }
    [Required, MaxLength(20)]
    public string DietType { get; set; } = string.Empty;
    [Required, MaxLength(20)]
    public string ActivityLevel { get; set; } = string.Empty;
}
