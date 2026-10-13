namespace VeganHelper.DAL.Entities;

public sealed class MealSuitableTime
{
    public long MealId { get; set; }
    public string MealTime { get; set; } = string.Empty;
}
