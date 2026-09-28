namespace VeganHelper.BLL.DTOs.HealthProfile;

public class BmiCalculationResult
{
    public decimal Bmi { get; set; }
    public string Category { get; set; } = string.Empty;
    public IdealWeightRange IdealWeightRange { get; set; } = new();
    public decimal DailyCalorieRecommendation { get; set; }
}

public class IdealWeightRange
{
    public decimal MinKg { get; set; }
    public decimal MaxKg { get; set; }
}
