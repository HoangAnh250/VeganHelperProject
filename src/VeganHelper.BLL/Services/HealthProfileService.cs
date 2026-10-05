using AutoMapper;
using FluentValidation;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.HealthProfile;
using VeganHelper.BLL.Exceptions;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class HealthProfileService(
    IHealthProfileRepository repository,
    IMapper mapper,
    IValidator<UpdateHealthProfileRequest> profileValidator,
    IValidator<DeclareAllergiesRequest> allergiesValidator) : IHealthProfileService
{
    public async Task<HealthProfileDto> GetHealthProfileAsync(long userId, CancellationToken ct = default)
    {
        var result = await repository.GetAsync(userId, ct);
        var dto = result.Profile is null ? new HealthProfileDto() : mapper.Map<HealthProfileDto>(result.Profile);
        dto.Allergies = mapper.Map<List<AllergyDto>>(result.Allergies);
        dto.AllergyIngredientIds = result.Allergies.Where(a => !a.IsCustom).Select(a => a.IngredientId).ToList();
        dto.CustomAllergies = result.Allergies.Where(a => a.IsCustom).Select(a => a.Name).ToList();
        return dto;
    }

    public async Task<UpdateHealthProfileResponse> UpdateHealthProfileAsync(long userId, UpdateHealthProfileRequest request, CancellationToken ct = default)
    {
        request.BiologicalSex = request.BiologicalSex?.Trim().ToLowerInvariant() ?? "";
        request.DietType = request.DietType?.Trim().ToLowerInvariant() ?? "";
        request.ActivityLevel = request.ActivityLevel?.Trim().ToLowerInvariant() ?? "";
        await profileValidator.ValidateAndThrowAsync(request, ct);
        var profile = mapper.Map<UserProfile>(request);
        var bmi = CalculateBmi(request.WeightKg, request.HeightCm);
        await repository.UpdateAsync(userId, profile, bmi, ct);
        return new UpdateHealthProfileResponse { CurrentBmi = bmi, BmiCategory = HasAdultReference(profile) ? GetBmiCategory(bmi) : null, EstimatedTdee = CalculateTdee(profile) };
    }

    public async Task<BmiCalculationResult> GetBmiResultAsync(long userId, CancellationToken ct = default)
    {
        var profile = (await repository.GetAsync(userId, ct)).Profile;
        if (profile?.HeightCm is not >= 100m or > 250m || profile.WeightKg is not >= 30m or > 200m)
            throw new ArgumentException("Please update valid height and weight before calculating BMI.");
        var heightM = profile.HeightCm.Value / 100m;
        var bmi = CalculateBmi(profile.WeightKg!.Value, profile.HeightCm.Value);
        return new BmiCalculationResult
        {
            Bmi = bmi, Category = HasAdultReference(profile) ? GetBmiCategory(bmi) : null,
            IdealWeightRange = HasAdultReference(profile)
                ? new IdealWeightRange { MinKg = Math.Round(18.5m * heightM * heightM, 2), MaxKg = Math.Round(24.9m * heightM * heightM, 2) }
                : null,
            DailyCalorieRecommendation = CalculateTdee(profile),
            NutritionSuggestions =
            [
                "Ăn đa dạng rau, trái cây và ngũ cốc nguyên hạt trong chế độ ăn cân bằng.",
                "Chọn nguồn đạm thực vật và thực phẩm tăng cường vi chất phù hợp với các dị ứng đã khai báo.",
                "Chú ý nguồn vitamin B12, canxi và sắt khi xây dựng chế độ ăn chay."
            ],
            NutritionSourceUrl = "https://www.nhs.uk/live-well/eat-well/how-to-eat-a-balanced-diet/the-vegan-diet/"
        };
    }

    public async Task<GetBmiHistoryResponse> GetBmiHistoryAsync(long userId, int pageIndex = 1, int pageSize = 100, CancellationToken ct = default)
    {
        Pagination.Validate(pageIndex, pageSize);
        var page = await repository.GetHistoryAsync(userId, DateTime.UtcNow.AddMonths(-6), pageIndex, pageSize, ct);
        return new GetBmiHistoryResponse
        {
            Items = mapper.Map<List<BmiHistoryDto>>(page.Items), TotalItems = page.TotalCount,
            TotalCount = checked((int)page.TotalCount), PageIndex = pageIndex, PageSize = pageSize, TotalPages = Pagination.TotalPages(page.TotalCount, pageSize)
        };
    }

    public async Task DeclareAllergiesAsync(long userId, DeclareAllergiesRequest request, CancellationToken ct = default)
    {
        await allergiesValidator.ValidateAndThrowAsync(request, ct);
        var ids = request.AllergyIngredientIds.Distinct().ToArray();
        var names = request.CustomAllergies.Select(n => n.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        await repository.ReplaceAllergiesAsync(userId, ids, names, ct);
    }

    public async Task<PagedResult<IngredientOptionDto>> GetIngredientsAsync(string? keyword, int pageIndex = 1, int pageSize = 20, CancellationToken ct = default)
    {
        Pagination.Validate(pageIndex, pageSize);
        keyword = keyword?.Trim();
        if (keyword?.Length > 100) throw new ArgumentException("Keyword must be at most 100 characters.");
        var page = await repository.SearchIngredientsAsync(keyword, pageIndex, pageSize, ct);
        return Pagination.Map<Ingredient, IngredientOptionDto>(page, pageIndex, pageSize, mapper);
    }

    public async Task<AllergyWarningsDto> GetPostWarningsAsync(long userId, long postId, CancellationToken ct = default)
    {
        var warnings = await repository.GetPostWarningsAsync(userId, postId, ct)
            ?? throw new NotFoundException("Post not found.");
        return new AllergyWarningsDto { PostId = postId, Allergens = mapper.Map<List<AllergyDto>>(warnings) };
    }

    private static decimal CalculateBmi(decimal kg, decimal cm) => Math.Round(kg / ((cm / 100m) * (cm / 100m)), 2);
    private static string GetBmiCategory(decimal bmi) => bmi < 18.5m ? "Underweight" : bmi < 25m ? "Normal" : bmi < 30m ? "Overweight" : "Obese";

    private static bool HasAdultReference(UserProfile profile) => profile.BirthDate is null
        || profile.BirthDate <= DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-18)
            && profile.BirthDate >= DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-120);

    private static decimal? CalculateTdee(UserProfile profile)
    {
        if (profile.HeightCm is null || profile.WeightKg is null || profile.BirthDate is null || profile.BiologicalSex is not ("male" or "female")) return null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var birthDate = profile.BirthDate.Value;
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age)) age--;
        if (age is < 18 or > 120) return null;
        var multiplier = profile.ActivityLevel switch { "sedentary" => 1.2m, "light" => 1.375m, "moderate" => 1.55m, "active" => 1.725m, "very_active" => 1.9m, _ => (decimal?)null };
        if (multiplier is null) return null;
        var bmr = 10m * profile.WeightKg.Value + 6.25m * profile.HeightCm.Value - 5m * age + (profile.BiologicalSex == "male" ? 5m : -161m);
        return Math.Round(bmr * multiplier.Value, 2);
    }
}
