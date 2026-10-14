namespace VeganHelper.BLL.DTOs.HealthProfile;

public class UpdateHealthProfileResponse
{
    public string Message { get; set; } = "Cập nhật hồ sơ sức khỏe thành công";
    public decimal CurrentBmi { get; set; }
    public string? BmiCategory { get; set; }
    public decimal? EstimatedTdee { get; set; }
}
