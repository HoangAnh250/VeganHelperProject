namespace VeganHelper.DAL.Entities;

public sealed class MealPlanSchedule
{
    public long Id { get; set; }
    public long MealPlanId { get; set; }
    public long MealId { get; set; }
    public byte DayOfWeek { get; set; }
    public string MealTime { get; set; } = string.Empty;
    public decimal PortionMultiplier { get; set; }
}
