using VeganHelper.BLL.DTOs.MealPlanning;
using VeganHelper.BLL.Exceptions;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services.MealPlanning;

public static class MealPlanGenerator
{
    public static WeeklyMenuDto Generate(MealPlanningInput input, DateOnly today, Random random,
        CancellationToken ct=default, decimal? targetOverride=null, MealCalorieSplitDto? mealCalorieSplit=null)
    {
        var split=mealCalorieSplit ?? MealCalorieSplitDto.LegacyDefault();
        if(!split.IsValid()) throw new ArgumentException("Meal calorie percentages must total 100%.");
        (string Time, decimal Ratio)[] slots =
        [
            ("breakfast",split.BreakfastPercent/100m),
            ("lunch",split.LunchPercent/100m),
            ("dinner",split.DinnerPercent/100m)
        ];
        var profile=input.Profile ?? throw new ArgumentException("Complete your health profile before generating a meal plan.");
        var maintenance=MaintenanceEnergy.Estimate(profile,today,20)
            ?? throw new ArgumentException("Generation requires age 20–120, valid height/weight, male/female energy inputs and a supported activity level.");
        var target=targetOverride ?? maintenance;
        if (profile.DietType is not ("vegan" or "lacto_ovo_vegetarian")) throw new ArgumentException("Unsupported diet type.");
        var excluded=AllergyFilter.ExcludedIngredients(input.Allergies,input.Ingredients,
            input.Meals.SelectMany(m=>m.Ingredients).Select(i=>i.IngredientId).ToHashSet());
        var eligible=input.Meals.Where(m=>m.Servings>0 && m.TotalCalories>0
            && m.DietType is "vegan" or "lacto_ovo_vegetarian"
            && (profile.DietType=="lacto_ovo_vegetarian" || m.DietType=="vegan")
            && m.Ingredients.Count>0 && m.Ingredients.All(i=>i.Quantity>0 && !excluded.Contains(i.IngredientId))).ToList();
        // Preserve reasonable serving sizes. Never relax allergies/diet to fill the week.
        var pools=slots.ToDictionary(s=>s.Time,s=>eligible.Where(m=>m.SuitableTimes.Contains(s.Time)
            && BaseCalories(m)*1.5m >= target*s.Ratio*.9m
            && BaseCalories(m)*.75m <= target*s.Ratio*1.1m).ToList());
        if (pools.Any(p=>p.Value.Count<7) || pools.Values.SelectMany(x=>x).Select(m=>m.Id).Distinct().Count()<21)
            throw new ConflictException("Not enough distinct suitable dishes within diet, allergy and calorie/portion limits for a full week.");

        List<MealDayDto>? best=null;
        var bestScore=decimal.MaxValue;
        for (var attempt=0; attempt<40; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var used=new HashSet<long>();
            var mainCounts=new Dictionary<long,int>();
            var days=new List<MealDayDto>();
            decimal score=0;
            for (byte day=1; day<=7; day++)
            {
                var result=new MealDayDto { DayIndex=day };
                foreach(var slot in slots)
                {
                    var desired=target*slot.Ratio;
                    var options=pools[slot.Time].Where(m=>!used.Contains(m.Id)).Select(m=>
                    {
                        var portion=Math.Round(Math.Clamp(desired/BaseCalories(m),.75m,1.5m),2);
                        var calories=Math.Round(BaseCalories(m)*portion,2);
                        var main=m.Ingredients.OrderByDescending(i=>i.Quantity).First().IngredientId;
                        var penalty=Math.Abs(calories-desired)/desired + Math.Abs(portion-1)*.04m
                            + mainCounts.GetValueOrDefault(main)*.02m;
                        return new { Meal=m, Portion=portion, Calories=calories, Main=main, Penalty=penalty,
                            Rank=penalty+(decimal)random.NextDouble()*.06m };
                    }).OrderBy(o=>o.Rank).FirstOrDefault();
                    if (options is null) break;
                    used.Add(options.Meal.Id);
                    mainCounts[options.Main]=mainCounts.GetValueOrDefault(options.Main)+1;
                    score+=options.Penalty;
                    result.Meals.Add(new ScheduledMealDto
                    {
                        MealId=options.Meal.Id, Name=options.Meal.Name, ImageUrl=options.Meal.ImageUrl, MealTime=slot.Time,
                        PortionMultiplier=options.Portion, Calories=options.Calories,
                        CookingSteps=options.Meal.CookingSteps,
                        Ingredients=options.Meal.Ingredients.Select(i=>new MealIngredientDto
                        {
                            IngredientId=i.IngredientId, Name=i.Name, Unit=i.Unit,
                            Quantity=IngredientQuantityFormatter.RoundForDisplay(
                                i.Quantity!.Value/options.Meal.Servings*options.Portion,i.Unit)
                        }).ToList()
                    });
                }
                result.TotalCalories=result.Meals.Sum(m=>m.Calories);
                if (result.Meals.Count!=3 || Math.Abs(result.TotalCalories-target)>target*.1m) break;
                days.Add(result);
            }
            if(days.Count==7 && score<bestScore) { best=days; bestScore=score; }
        }
        if(best is null) throw new ConflictException("No complete weekly menu was found within the calorie and portion limits. Try a broader suitable dish catalog.");
        return new WeeklyMenuDto
        {
            DietType=profile.DietType, TargetDailyCalories=target, MealCalorieSplit=split,
            EnergyProfile=new EnergyProfileDto
            {
                EstimatedOn=today, BirthDate=profile.BirthDate!.Value,
                HeightCm=profile.HeightCm!.Value, WeightKg=profile.WeightKg!.Value,
                BiologicalSex=profile.BiologicalSex!, ActivityLevel=profile.ActivityLevel!
            },
            Bmi=Math.Round(profile.WeightKg!.Value/(profile.HeightCm!.Value/100m)/(profile.HeightCm.Value/100m),2),
            Allergies=input.Allergies.Select(a=>a.Name).ToList(), Days=best,
            Notes=["Estimated maintenance calories; no automatic weight-loss or weight-gain prescription.",
                "Sample dishes are not dietitian reviewed. Calories alone do not establish nutritional adequacy or guarantee a healthy BMI.",
                "Allergy filtering uses ingredient IDs and conservative supported families; verify packaged ingredients and cross-contact separately.",
                "This adult energy estimate is not intended for pregnancy, breastfeeding or medical nutrition treatment."]
        };
    }

    private static decimal BaseCalories(MealCandidate meal)=>meal.TotalCalories!.Value/meal.Servings;
}
