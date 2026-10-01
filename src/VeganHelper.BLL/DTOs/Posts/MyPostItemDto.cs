using System;

namespace VeganHelper.BLL.DTOs.Posts;

public class MyPostItemDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Content { get; set; }
    public string? PostType { get; set; }
    public string? CategoryName { get; set; }
    public int ViewCount { get; set; }
}
