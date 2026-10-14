using System.ComponentModel.DataAnnotations;

namespace VeganHelper.BLL.DTOs.HealthProfile;

public class DeclareAllergiesRequest
{
    [Required, MaxLength(100)]
    public List<long> AllergyIngredientIds { get; set; } = new();
    [Required, MaxLength(50)]
    public List<string> CustomAllergies { get; set; } = new();
}
