namespace VeganHelper.BLL.DTOs.Posts;

using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

public class CreatePostRequest
{
    public string Title { get; set; } = string.Empty;
    public string PostType { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string DifficultyLevel { get; set; } = string.Empty;
    public int? PrepTimeMins { get; set; }
    public int? CookingTimeMins { get; set; }
    public string? DietType { get; set; }
    public IFormFileCollection? MediaFiles { get; set; }
    public string? IngredientsJson { get; set; }
    public string? StepsJson { get; set; }
}

public class IngredientDto
{
    public long IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public class StepDto
{
    public int StepNumber { get; set; }
    public string Description { get; set; } = string.Empty;
}
