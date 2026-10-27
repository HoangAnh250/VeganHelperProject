namespace VeganHelper.DAL.Entities;

public sealed class MealCookingStep
{
    public long MealId { get; set; }
    public short StepNumber { get; set; }
    public string Instruction { get; set; } = string.Empty;
}
