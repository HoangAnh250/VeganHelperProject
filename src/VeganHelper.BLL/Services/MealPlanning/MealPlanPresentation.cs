using VeganHelper.BLL.DTOs.MealPlanning;

namespace VeganHelper.BLL.Services.MealPlanning;

public static class MealPlanPresentation
{
    public static void Apply(WeeklyMenuDto menu, IReadOnlyDictionary<long, string> imageUrls)
    {
        foreach (var meal in menu.Days.SelectMany(day => day.Meals))
        {
            meal.ImageUrl = imageUrls.GetValueOrDefault(meal.MealId);
            foreach (var ingredient in meal.Ingredients)
                ingredient.Quantity = IngredientQuantityFormatter.RoundForDisplay(ingredient.Quantity, ingredient.Unit);
        }
    }
}
