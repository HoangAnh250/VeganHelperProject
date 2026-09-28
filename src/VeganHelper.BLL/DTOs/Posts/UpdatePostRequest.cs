using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace VeganHelper.BLL.DTOs.Posts;

public class UpdatePostRequest
{
    public string Title { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? DifficultyLevel { get; set; }
    public int? PrepTimeMins { get; set; }
    public int? CookingTimeMins { get; set; }
    public string? DietType { get; set; }
    
    public List<IFormFile>? MediaFilesToAdd { get; set; }
    public List<long>? MediaIdsToRemove { get; set; }
    
    public string? IngredientsJson { get; set; }
    public string? StepsJson { get; set; }
}
