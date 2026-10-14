namespace VeganHelper.BLL.DTOs.Posts;

public sealed class SearchPostsRequest
{
    public string Keyword { get; set; } = string.Empty;
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
