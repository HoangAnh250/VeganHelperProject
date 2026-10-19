namespace VeganHelper.BLL.Exceptions;

public sealed class MealPlanAccessRequiredException(string message, string errorCode = "MEAL_PLAN_ACCESS_REQUIRED")
    : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}
