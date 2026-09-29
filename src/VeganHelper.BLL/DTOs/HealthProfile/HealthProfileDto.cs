namespace VeganHelper.BLL.DTOs.HealthProfile;

public class HealthProfileDto
{
    public decimal? HeightCm { get; set; }
    public decimal? WeightKg { get; set; }
    public string? BiologicalSex { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string DietType { get; set; } = string.Empty;
    public string? ActivityLevel { get; set; }
    public decimal? CurrentBmi { get; set; }
    
    public List<long> AllergyIngredientIds { get; set; } = new();
}
