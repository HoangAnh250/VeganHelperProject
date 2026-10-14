namespace VeganHelper.BLL.DTOs.Posts;

public class PostMediaDto
{
    public long Id { get; set; }
    public int DisplayOrder { get; set; }
    public string MediaUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}
