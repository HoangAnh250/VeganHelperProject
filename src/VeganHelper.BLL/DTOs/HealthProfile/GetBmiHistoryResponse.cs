namespace VeganHelper.BLL.DTOs.HealthProfile;

public class GetBmiHistoryResponse
{
    public List<BmiHistoryDto> History { get; set; } = new();
}

public class BmiHistoryDto
{
    public DateTime Timestamp { get; set; }
    public decimal WeightKg { get; set; }
    public decimal Bmi { get; set; }
}
