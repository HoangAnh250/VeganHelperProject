namespace VeganHelper.BLL.Services.MealPlanning;

public static class IngredientQuantityFormatter
{
    public static decimal RoundForDisplay(decimal quantity, string unit)
    {
        if (quantity <= 0) return quantity;

        var normalizedUnit = unit.Trim().ToLowerInvariant();
        var increment = normalizedUnit switch
        {
            "g" or "gram" or "grams" or "ml" or "milliliter" or "milliliters" =>
                quantity >= 50 ? 10m : quantity >= 10 ? 5m : 1m,
            "piece" or "pieces" or "egg" or "eggs" or "clove" or "cloves" => 1m,
            _ => 0.5m
        };

        var rounded = Math.Round(quantity / increment, 0, MidpointRounding.AwayFromZero) * increment;
        return rounded > 0 ? rounded : increment;
    }
}
