namespace VeganHelper.BLL.DTOs.Posts;

public class GetMyPostsRequest
{
    public string? Status { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
