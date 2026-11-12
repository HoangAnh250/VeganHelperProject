namespace VeganHelper.BLL.DTOs.Posts;

public sealed class SearchPostsRequest
{
    [System.ComponentModel.DataAnnotations.DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Keyword { get; set; }
    public int? CategoryId { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
