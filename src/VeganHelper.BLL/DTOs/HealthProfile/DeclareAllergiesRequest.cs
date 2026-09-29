namespace VeganHelper.BLL.DTOs.HealthProfile;

public class DeclareAllergiesRequest
{
    public List<long> AllergyIngredientIds { get; set; } = new();
    public List<string> CustomAllergies { get; set; } = new();
}
