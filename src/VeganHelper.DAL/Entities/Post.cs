namespace VeganHelper.DAL.Entities;

public sealed class Post
{
    public int ContentRevision { get; set; } = 1;
    public long Id { get; set; }
    public long AuthorId { get; set; }
    public string PostType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string? DifficultyLevel { get; set; }
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

    // Navigation Properties
    public ICollection<PostCategory> PostCategories { get; set; } = new List<PostCategory>();
    public ICollection<PostMedia> Media { get; set; } = new List<PostMedia>();
    public ICollection<PostIngredient> PostIngredients { get; set; } = new List<PostIngredient>();
    public ICollection<PostStep> PostSteps { get; set; } = new List<PostStep>();
}
