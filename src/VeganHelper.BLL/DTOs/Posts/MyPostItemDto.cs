using System;

namespace VeganHelper.BLL.DTOs.Posts;

public class MyPostItemDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ThumbnailUrl { get; set; }
}
