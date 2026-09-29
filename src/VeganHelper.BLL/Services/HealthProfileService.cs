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

    public async Task<BmiCalculationResult?> GetBmiResultAsync(long userId)
    {
        var profile = await _dbContext.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile == null || profile.HeightCm <= 0 || profile.WeightKg <= 0 || string.IsNullOrEmpty(profile.BiologicalSex) || string.IsNullOrEmpty(profile.ActivityLevel))
        {
            return null;
        }

        var bmi = CalculateBmi(profile.WeightKg.Value, profile.HeightCm.Value);
        var tdee = CalculateTdee(profile.WeightKg.Value, profile.HeightCm.Value, profile.BirthDate.Value, profile.BiologicalSex, profile.ActivityLevel);
        
        var heightM = profile.HeightCm.Value / 100m;
        var minKg = 18.5m * heightM * heightM;
        var maxKg = 24.9m * heightM * heightM;

        return new BmiCalculationResult
        {
            Bmi = bmi,
            Category = GetBmiCategory(bmi),
            IdealWeightRange = new IdealWeightRange
            {
                MinKg = Math.Round(minKg, 2),
                MaxKg = Math.Round(maxKg, 2)
            },
            DailyCalorieRecommendation = tdee
        };
    }

    public async Task<GetBmiHistoryResponse> GetBmiHistoryAsync(long userId)
    {
        var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
        
        var history = await _dbContext.BmiHistories
            .Where(x => x.UserId == userId && x.RecordedAt >= sixMonthsAgo)
            .OrderBy(x => x.RecordedAt)
            .Select(x => new BmiHistoryDto
            {
                Timestamp = x.RecordedAt,
                WeightKg = x.WeightKg,
                Bmi = x.BmiValue
            })
            .ToListAsync();

        return new GetBmiHistoryResponse { History = history };
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
