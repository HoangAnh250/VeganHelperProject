namespace VeganHelper.BLL.DTOs.HealthProfile;

public sealed class AllergyDto
{
    public long IngredientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsCustom { get; set; }
}

public sealed class IngredientOptionDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DefaultUnit { get; set; } = string.Empty;
}

public sealed class AllergyWarningsDto
{
    public long PostId { get; set; }
    public bool HasAllergyWarning => Allergens.Count > 0;
    public List<AllergyDto> Allergens { get; set; } = new();
}
