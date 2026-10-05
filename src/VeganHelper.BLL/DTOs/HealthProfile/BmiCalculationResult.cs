namespace VeganHelper.BLL.DTOs.HealthProfile;

public class BmiCalculationResult
{
    public decimal Bmi { get; set; }
    public string? Category { get; set; }
    public IdealWeightRange? IdealWeightRange { get; set; }
    public decimal? DailyCalorieRecommendation { get; set; }
    public List<string> NutritionSuggestions { get; set; } = new();
    public string NutritionSourceUrl { get; set; } = string.Empty;
}

public class IdealWeightRange
{
    public decimal MinKg { get; set; }
    public decimal MaxKg { get; set; }
}
