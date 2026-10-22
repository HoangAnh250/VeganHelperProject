using VeganHelper.BLL.Services.MealPlanning;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;
using VeganHelper.BLL.Exceptions;
using System.Text.Json;
using System.Globalization;

namespace VeganHelper.UnitTests;

public class MealPlanningTests
{
    private static MealPlanningInput Catalog(string diet = "vegan") => new()
    {
        Profile = new UserProfile { HeightCm=165, WeightKg=60, BirthDate=new(1996,1,1), BiologicalSex="female", ActivityLevel="moderate", DietType=diet },
        Ingredients = [new Ingredient { Id=1, Name="Cooked rice" }, new Ingredient { Id=2, Name="Peanut butter" }, new Ingredient { Id=3, Name="Peanuts" }],
        Meals = Enumerable.Range(1, 70).Select(id => new MealCandidate
        {
            Id=id, Name=$"Dish {id}", DietType="vegan", Servings=2, TotalCalories=id <= 20 ? 1100 : 1400,
            SuitableTimes=id <= 20 ? ["breakfast"] : ["lunch","dinner"],
            Ingredients=[new RecipeIngredient { IngredientId=1, Name="Cooked rice", Quantity=400, Unit="g" }]
        }).ToList()
    };

    [Fact]
    public void Generate_CoversAllSlotsWithoutRepetitionAndScalesPerServing()
    {
        var result = MealPlanGenerator.Generate(Catalog(), new DateOnly(2026,10,12), new Random(42));
        Assert.Equal(7, result.Days.Count);
        Assert.Equal(21, result.Days.SelectMany(d=>d.Meals).Select(m=>m.MealId).Distinct().Count());
        Assert.All(result.Days, day => {
            Assert.Equal(new[]{"breakfast","lunch","dinner"}, day.Meals.Select(m=>m.MealTime));
            Assert.InRange(day.TotalCalories, result.TargetDailyCalories*.9m, result.TargetDailyCalories*1.1m);
            Assert.All(day.Meals, meal=> {
                Assert.InRange(meal.PortionMultiplier,.75m,1.5m);
                Assert.Equal(Math.Round(200*meal.PortionMultiplier,3), meal.Ingredients.Single().Quantity);
            });
        });
    }

    [Fact]
    public void Generate_ExposesMatchingMealImageWithoutChangingSelection()
    {
        var input=Catalog();
        foreach(var meal in input.Meals) meal.ImageUrl=$"https://media.example/meals/{meal.Id}.jpg";

        var menu=MealPlanGenerator.Generate(input,new(2026,10,12),new Random(42));

        Assert.All(menu.Days.SelectMany(day=>day.Meals),meal=>
            Assert.Equal($"https://media.example/meals/{meal.MealId}.jpg",meal.ImageUrl));
    }

    [Fact]
    public void Generate_ExpandsPeanutAllergyAndRejectsAnimalMealsForVegan()
    {
        var input=Catalog();
        input.Allergies=[new AllergyProjection { IngredientId=3, Name="Peanuts" }];
        input.Meals[0].Ingredients=[new RecipeIngredient { IngredientId=2, Name="Peanut butter", Quantity=100, Unit="g" }];
        input.Meals[1].DietType="lacto_ovo_vegetarian";
        var ids=MealPlanGenerator.Generate(input,new(2026,10,12),new(2)).Days.SelectMany(d=>d.Meals).Select(m=>m.MealId);
        Assert.DoesNotContain(1L,ids);
        Assert.DoesNotContain(2L,ids);
    }

    [Fact]
    public void UnknownCustomAllergy_DoesNotSilentlyGenerate()
    {
        var input=Catalog();
        input.Allergies=[new AllergyProjection { IngredientId=999, Name="Mystery allergy", IsCustom=true }];
        Assert.Throws<ConflictException>(()=>MealPlanGenerator.Generate(input,new(2026,10,12),new(2)));
    }

    [Fact]
    public void UnknownAliasReselectedById_IsStillUnresolved()
    {
        var input=Catalog();
        input.Ingredients.Add(new Ingredient { Id=999,Name="arachide" });
        input.Allergies=[new AllergyProjection { IngredientId=999,Name="arachide",IsCustom=false }];
        Assert.Throws<ConflictException>(()=>MealPlanGenerator.Generate(input,new(2026,10,12),new(2)));
    }

    [Fact]
    public void OtherUnusedCustomAlias_IsNotResolutionEvidence()
    {
        var input=Catalog();
        input.Ingredients.AddRange([new Ingredient { Id=999,Name="arachide" },new Ingredient { Id=1000,Name="arachide powder" }]);
        input.Allergies=[new AllergyProjection { IngredientId=999,Name="arachide",IsCustom=true }];
        Assert.Throws<ConflictException>(()=>MealPlanGenerator.Generate(input,new(2026,10,12),new(2)));
    }

    [Fact]
    public void InsufficientBreakfasts_DoesNotReturnPartialPlan()
    {
        var input=Catalog();
        input.Meals=input.Meals.Where(m=>m.Id>15).ToList();
        Assert.Throws<ConflictException>(()=>MealPlanGenerator.Generate(input,new(2026,10,12),new(2)));
    }

    [Theory]
    [InlineData("other", "moderate")]
    [InlineData("female", "unknown")]
    public void InvalidEnergyInputs_AreRejected(string sex,string activity)
    {
        var input=Catalog(); input.Profile!.BiologicalSex=sex; input.Profile.ActivityLevel=activity;
        Assert.Throws<ArgumentException>(()=>MealPlanGenerator.Generate(input,new(2026,10,12),new(2)));
    }

    [Fact]
    public void Under20_IsNotAssignedAdultMaintenancePlan()
    {
        var input=Catalog(); input.Profile!.BirthDate=new(2008,1,1);
        Assert.Throws<ArgumentException>(()=>MealPlanGenerator.Generate(input,new(2026,10,12),new(2)));
    }

    [Theory]
    [InlineData("vegan", "female", "moderate")]
    [InlineData("vegan", "male", "light")]
    [InlineData("lacto_ovo_vegetarian", "male", "moderate")]
    public void Current200DishSeed_GeneratesCompleteCalorieBoundedMenus(string diet,string sex,string activity)
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"meal-planning-seed.json")));
        var root=json.RootElement;
        var input=Catalog(diet);
        input.Profile!.BiologicalSex=sex; input.Profile.ActivityLevel=activity;
        var keyed=root.GetProperty("ingredients").EnumerateArray().Select((i,n)=>new
        { Key=i.GetProperty("key").GetString()!, Ingredient=new Ingredient { Id=n+1,Name=i.GetProperty("name").GetString()! } }).ToDictionary(x=>x.Key,x=>x.Ingredient);
        input.Ingredients=keyed.Values.ToList();
        input.Meals=root.GetProperty("meals").EnumerateArray().Select((m,n)=>new MealCandidate
        {
            Id=n+1,Name=m.GetProperty("name").GetString()!,DietType=m.GetProperty("diet_type").GetString()!,
            Servings=m.GetProperty("servings").GetDecimal(),
            TotalCalories=decimal.Parse(m.GetProperty("total_calories").GetString()!,CultureInfo.InvariantCulture),
            SuitableTimes=m.GetProperty("slots").EnumerateArray().Select(s=>s.GetString()!).ToList(),
            Ingredients=m.GetProperty("recipe").EnumerateObject().Select(i=>new RecipeIngredient
            {
                IngredientId=keyed[i.Name].Id,Name=keyed[i.Name].Name,Unit="g",
                Quantity=decimal.Parse(i.Value.GetString()!,CultureInfo.InvariantCulture)
            }).ToList()
        }).ToList();
        foreach(var seed in new[]{1,2,3})
        {
            var menu=MealPlanGenerator.Generate(input,new(2026,10,12),new(seed));
            Assert.Equal(7,menu.Days.Count);
            Assert.Equal(21,menu.Days.SelectMany(d=>d.Meals).Select(m=>m.MealId).Distinct().Count());
            Assert.All(menu.Days,d=>Assert.InRange(d.TotalCalories,menu.TargetDailyCalories*.9m,menu.TargetDailyCalories*1.1m));
            if(diet=="vegan") Assert.All(menu.Days.SelectMany(d=>d.Meals),m=>Assert.Equal("vegan",input.Meals.Single(c=>c.Id==m.MealId).DietType));
        }
    }

    [Fact]
    public void UnachievableCalories_DoesNotUseExcessivePortions()
    {
        var input=Catalog(); input.Profile!.WeightKg=200; input.Profile.ActivityLevel="very_active";
        Assert.Throws<ConflictException>(()=>MealPlanGenerator.Generate(input,new(2026,10,12),new(2)));
    }

    [Fact]
    public void CancelledGeneration_StopsBeforeSearch()
    {
        using var cts=new CancellationTokenSource(); cts.Cancel();
        Assert.Throws<OperationCanceledException>(()=>MealPlanGenerator.Generate(Catalog(),new(2026,10,12),new(2),cts.Token));
    }

    [Fact]
    public void LactoOvoDiet_CanUseEggDairyDishes()
    {
        var input=Catalog("lacto_ovo_vegetarian");
        foreach(var m in input.Meals) m.DietType="lacto_ovo_vegetarian";
        Assert.Equal(7,MealPlanGenerator.Generate(input,new(2026,10,12),new(2)).Days.Count);
    }
}
