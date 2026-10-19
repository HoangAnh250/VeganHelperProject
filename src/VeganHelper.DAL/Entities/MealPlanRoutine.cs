namespace VeganHelper.DAL.Entities;

public sealed class MealPlanRoutine
{
    public long MealPlanId { get; set; }
    public long UserId { get; set; }
    public long RootPlanId { get; set; }
    public long? PreviousPlanId { get; set; }
    public string Mode { get; set; }="maintenance";
    public decimal? TargetWeightKg { get; set; }
    public DateOnly? TargetDate { get; set; }
    public bool EligibilityConfirmed { get; set; }
    public string TimeZoneId { get; set; }="UTC";
    public bool RemindersEnabled { get; set; }
    public TimeOnly BreakfastTime { get; set; }=new(7,30);
    public TimeOnly LunchTime { get; set; }=new(12,0);
    public TimeOnly DinnerTime { get; set; }=new(18,30);
    public string State { get; set; }="active";
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class MealPlanEntry
{
    public long Id { get; set; }
    public long MealPlanId { get; set; }
    public DateOnly Date { get; set; }
    public string MealTime { get; set; }="";
    public string Status { get; set; }="unlogged";
    public string? Note { get; set; }
    public DateTime? LoggedAt { get; set; }
    public DateTime DueAt { get; set; }
}
