namespace VeganHelper.DAL.Models;
using System;

public class PostFeedProjection
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string PostType { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public long ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
