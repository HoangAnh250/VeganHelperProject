namespace VeganHelper.BLL.DTOs.Posts;

public class PostMediaDto
{
    public string MediaUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}
