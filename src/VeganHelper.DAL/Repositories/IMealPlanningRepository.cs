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
    public string? ImageUrl { get; set; }
    public List<string> CookingSteps { get; set; } = [];
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
    Task<IReadOnlyDictionary<long, string>> GetImageUrlsAsync(IReadOnlyCollection<long> mealIds, CancellationToken ct);
    Task<IReadOnlyDictionary<long, List<string>>> GetCookingStepsAsync(IReadOnlyCollection<long> mealIds, CancellationToken ct);
    Task SaveAsync(MealPlan plan, IReadOnlyList<MealPlanSchedule> schedule, CancellationToken ct);
    Task<bool> HasOverlappingPlanAsync(long userId, DateOnly startDate, DateOnly endDate, long? exceptPlanId, CancellationToken ct);
    Task<MealPlan?> ArchiveAsync(long userId, long id, bool trialOnly, DateTime updatedAt, CancellationToken ct);
    Task<MealPlan?> ReplaceGeneratedAsync(long userId, long id, MealPlan replacement, IReadOnlyList<MealPlanSchedule> schedule, CancellationToken ct);
    Task<bool> TrySaveTrialAsync(MealPlan plan, IReadOnlyList<MealPlanSchedule> schedule, int generationLimit, CancellationToken ct);
    Task<int> CountTrialGenerationsAsync(long userId, CancellationToken ct);
    Task<MealPlan?> GetAsync(long userId, long id, bool trialOnly, CancellationToken ct);
    Task<MealPlan?> UpdateNameAsync(long userId, long id, string name, bool trialOnly, DateTime updatedAt, CancellationToken ct);
    Task<DatabasePage<MealPlan>> ListAsync(long userId, int pageIndex, int pageSize, bool trialOnly, CancellationToken ct);
}
