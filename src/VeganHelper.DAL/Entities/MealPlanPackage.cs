namespace VeganHelper.DAL.Entities;

public sealed class MealPlanPackage
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DurationMonths { get; set; }
    public int AmountVnd { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
