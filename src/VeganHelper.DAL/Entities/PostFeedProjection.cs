namespace VeganHelper.DAL.Entities;
using System;

public class PostFeedProjection
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? MediaUrl { get; set; }
    public string? MediaType { get; set; }
    public string? CategoryName { get; set; }
    public string? DietType { get; set; }
    public long LikeCount { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public long ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public long CommentCount { get; set; }
}
