namespace VeganHelper.BLL.DTOs.Posts;

public class GetFeedRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int? CategoryId { get; set; }
    public string? DifficultyLevel { get; set; }
    public string? DietType { get; set; }
    public int? PrepTimeMax { get; set; }
}
