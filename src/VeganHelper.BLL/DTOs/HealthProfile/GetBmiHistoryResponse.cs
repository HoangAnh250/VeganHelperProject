namespace VeganHelper.BLL.DTOs.HealthProfile;

public class GetBmiHistoryResponse : VeganHelper.BLL.DTOs.PagedResult<BmiHistoryDto>
{
    public IEnumerable<BmiHistoryDto> History => Items;
}

public class BmiHistoryDto
{
    public DateTime Timestamp { get; set; }
    public decimal WeightKg { get; set; }
    public decimal Bmi { get; set; }
}
