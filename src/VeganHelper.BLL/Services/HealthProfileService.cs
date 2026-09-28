using Microsoft.EntityFrameworkCore;
using VeganHelper.BLL.DTOs.HealthProfile;
using VeganHelper.DAL.Data;
using VeganHelper.DAL.Models;

namespace VeganHelper.BLL.Services;

public class HealthProfileService : IHealthProfileService
{
    private readonly AppDbContext _dbContext;

    public HealthProfileService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthProfileDto> GetHealthProfileAsync(long userId)
    {
        var profile = await _dbContext.UserProfiles
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (profile == null)
        {
            return new HealthProfileDto();
        }

        var allergyIds = await _dbContext.UserAllergies
            .Where(x => x.UserId == userId)
            .Select(x => x.IngredientId)
            .ToListAsync();

        return new HealthProfileDto
        {
            HeightCm = profile.HeightCm,
            WeightKg = profile.WeightKg,
            BiologicalSex = profile.BiologicalSex,
            BirthDate = profile.BirthDate,
            DietType = profile.DietType,
            ActivityLevel = profile.ActivityLevel,
            CurrentBmi = profile.CurrentBmi,
            AllergyIngredientIds = allergyIds
        };
    }

    public async Task<UpdateHealthProfileResponse> UpdateHealthProfileAsync(long userId, UpdateHealthProfileRequest request)
    {
        var profile = await _dbContext.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile == null)
        {
            profile = new UserProfile 
            { 
                UserId = userId,
                DisplayName = "User" // Typically handled on register
            };
            _dbContext.UserProfiles.Add(profile);
        }

        // 1. Update basic info
        profile.HeightCm = request.HeightCm;
        profile.WeightKg = request.WeightKg;
        profile.BiologicalSex = request.BiologicalSex;
        profile.BirthDate = request.BirthDate;
        profile.DietType = request.DietType;
        profile.ActivityLevel = request.ActivityLevel;
        profile.UpdatedAt = DateTime.UtcNow;

        // 2. Calculate BMI
        var bmi = CalculateBmi(request.WeightKg, request.HeightCm);
        profile.CurrentBmi = bmi;

        // 3. Log History
        var history = new BmiHistory
        {
            UserId = userId,
            HeightCm = request.HeightCm,
            WeightKg = request.WeightKg,
            BmiValue = bmi,
            RecordedAt = DateTime.UtcNow
        };
        _dbContext.BmiHistories.Add(history);

        await _dbContext.SaveChangesAsync();

        // 4. Calculate TDEE
        var tdee = CalculateTdee(request.WeightKg, request.HeightCm, request.BirthDate, request.BiologicalSex, request.ActivityLevel);
        
        return new UpdateHealthProfileResponse
        {
            Message = "Cập nhật hồ sơ sức khỏe thành công",
            CurrentBmi = bmi,
            BmiCategory = GetBmiCategory(bmi),
            EstimatedTdee = tdee
        };
    }

    public async Task DeclareAllergiesAsync(long userId, DeclareAllergiesRequest request)
    {
        // 1. Delete old allergies
        var existingAllergies = await _dbContext.UserAllergies.Where(x => x.UserId == userId).ToListAsync();
        _dbContext.UserAllergies.RemoveRange(existingAllergies);

        var ingredientIdsToAdd = new HashSet<long>();

        // 2. Process existing ingredient IDs
        if (request.AllergyIngredientIds != null && request.AllergyIngredientIds.Any())
        {
            var validIds = await _dbContext.Ingredients
                .Where(i => request.AllergyIngredientIds.Contains(i.Id))
                .Select(i => i.Id)
                .ToListAsync();
            
            foreach (var id in validIds)
            {
                ingredientIdsToAdd.Add(id);
            }
        }

        // 3. Process Custom Allergies
        if (request.CustomAllergies != null && request.CustomAllergies.Any())
        {
            foreach (var custom in request.CustomAllergies)
            {
                var trimmed = custom.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var existingIngredient = await _dbContext.Ingredients
                    .FirstOrDefaultAsync(i => i.Name.ToLower() == trimmed.ToLower());

                if (existingIngredient != null)
                {
                    ingredientIdsToAdd.Add(existingIngredient.Id);
                }
                else
                {
                    var newIngredient = new Ingredient
                    {
                        Name = trimmed,
                        DefaultUnit = "custom"
                    };
                    _dbContext.Ingredients.Add(newIngredient);
                    await _dbContext.SaveChangesAsync();
                    ingredientIdsToAdd.Add(newIngredient.Id);
                }
            }
        }

        // 4. Add new user allergies
        foreach (var id in ingredientIdsToAdd)
        {
            _dbContext.UserAllergies.Add(new UserAllergy
            {
                UserId = userId,
                IngredientId = id,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync();
    }

    private decimal CalculateBmi(decimal weightKg, decimal heightCm)
    {
        if (heightCm <= 0) return 0;
        var heightM = heightCm / 100m;
        var bmi = weightKg / (heightM * heightM);
        return Math.Round(bmi, 2);
    }

    private string GetBmiCategory(decimal bmi)
    {
        if (bmi < 18.5m) return "Underweight";
        if (bmi < 25m) return "Normal";
        if (bmi < 30m) return "Overweight";
        return "Obese";
    }

    private decimal CalculateTdee(decimal weightKg, decimal heightCm, DateOnly birthDate, string biologicalSex, string activityLevel)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age)) age--;
        
        if(age < 0) age = 0;

        // BMR (Mifflin-St Jeor)
        decimal bmr = (10m * weightKg) + (6.25m * heightCm) - (5m * age);
        if (string.Equals(biologicalSex, "male", StringComparison.OrdinalIgnoreCase))
        {
            bmr += 5m;
        }
        else
        {
            bmr -= 161m; // For female and others as fallback
        }

        decimal multiplier = activityLevel?.ToLower() switch
        {
            "sedentary" => 1.2m,
            "light" => 1.375m,
            "moderate" => 1.55m,
            "active" => 1.725m,
            "very_active" => 1.9m,
            _ => 1.2m
        };

        return Math.Round(bmr * multiplier, 2);
    }
}
