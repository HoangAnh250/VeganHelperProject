using System;
using System.Collections.Generic;

namespace VeganHelper.BLL.DTOs.Posts;

public class PostDetailDto
{
    public long Id { get; set; }
    public long AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string PostType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public int CategoryId { get; set; }
    public string? DifficultyLevel { get; set; }
    public int? PrepTimeMins { get; set; }
    public int? CookingTimeMins { get; set; }
    public string? DietType { get; set; }
    public string Status { get; set; } = string.Empty;
    public long ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<PostMediaDto> Media { get; set; } = new();
    public List<PostIngredientDto> Ingredients { get; set; } = new();
    public List<PostStepDto> Steps { get; set; } = new();
}
