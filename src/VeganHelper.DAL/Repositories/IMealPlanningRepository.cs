using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Repositories;

public sealed class RecipeIngredient
{
    public long IngredientId { get; set; }
    public string Name { get; set; } = "";
    public decimal? Quantity { get; set; }
    public string Unit { get; set; } = "";
}

public sealed class MealCandidate
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string DietType { get; set; } = "";
    public decimal Servings { get; set; }
    public decimal? TotalCalories { get; set; }
    public List<string> SuitableTimes { get; set; } = [];
    public List<RecipeIngredient> Ingredients { get; set; } = [];
}

public sealed class MealPlanningInput
{
    public UserProfile? Profile { get; set; }
    public List<AllergyProjection> Allergies { get; set; } = [];
    public List<Ingredient> Ingredients { get; set; } = [];
    public List<MealCandidate> Meals { get; set; } = [];
}

public interface IMealPlanningRepository
{
    Task<MealPlanningInput> LoadAsync(long userId, CancellationToken ct);
    Task SaveAsync(MealPlan plan, IReadOnlyList<MealPlanSchedule> schedule, CancellationToken ct);
    Task<MealPlan?> GetAsync(long userId, long id, CancellationToken ct);
    Task<DatabasePage<MealPlan>> ListAsync(long userId, int pageIndex, int pageSize, CancellationToken ct);
}
