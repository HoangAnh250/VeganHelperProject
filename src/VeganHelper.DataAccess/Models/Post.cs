namespace VeganHelper.DataAccess.Models;

public sealed class Post
{
    public long Id { get; set; }
    public long AuthorId { get; set; }
    public string PostType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string? MealType { get; set; }
    public int? PrepTimeMins { get; set; }
    public int? CookingTimeMins { get; set; }
    public int? Servings { get; set; }
    public decimal? CaloriesPerServing { get; set; }
    public string? DietType { get; set; }
    public bool IngredientsVerified { get; set; }
    public string Status { get; set; } = string.Empty;
    public long ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
