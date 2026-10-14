using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using VeganHelper.BLL.DTOs.MealPlanning;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class MealPlanningApiTests(Member2ApiFixture fixture)
{
    private async Task<long> SeedAsync(bool catalog=true)
    {
        await using var db=fixture.Context();
        var key=Guid.NewGuid().ToString("N");
        var user=new User { Username="plan_"+key,Email=key+"@example.invalid",RoleId=1,CreatedAt=DateTime.UtcNow };
        db.Users.Add(user); await db.SaveChangesAsync();
        db.UserProfiles.Add(new UserProfile
        { UserId=user.Id,DisplayName="Plan test",HeightCm=165,WeightKg=60,BirthDate=new(1996,1,1),
            BiologicalSex="female",ActivityLevel="moderate",DietType="vegan" });
        if(catalog)
        {
            var ingredient=new Ingredient { Name="Rice " + key,DefaultUnit="g",CaloriesPer100g=130 };
            db.Ingredients.Add(ingredient); await db.SaveChangesAsync();
            for(var n=0;n<35;n++)
            {
                var meal=new Meal { OwnerUserId=user.Id,ComboName=$"Test {key} {n}",DietType="vegan",Servings=1,
                    TotalCalories=n<10?550:700,CreatedAt=DateTime.UtcNow };
                db.Meals.Add(meal); await db.SaveChangesAsync();
                db.MealSuitableTimes.AddRange((n<10?new[]{"breakfast"}:new[]{"lunch","dinner"})
                    .Select(time=>new MealSuitableTime { MealId=meal.Id,MealTime=time }));
                db.MealIngredients.Add(new MealIngredient { MealId=meal.Id,IngredientId=ingredient.Id,CalculatedQuantity=300,Unit="g" });
            }
        }
        await db.SaveChangesAsync(); return user.Id;
    }

    [Fact]
    public async Task Generate_GuestIsUnauthorized()
    {
        using var client=fixture.Factory.Client();
        var response=await client.PostAsJsonAsync("/api/MealPlans/generate",new { startDate=DateOnly.FromDateTime(DateTime.UtcNow) });
        Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
    }

    [Fact]
    public async Task Generate_Persists21SlotsAndFrozenSnapshotAndEnforcesOwnership()
    {
        var owner=await SeedAsync(); var other=await SeedAsync(false);
        using var client=fixture.Factory.Client(owner);
        var response=await client.PostAsJsonAsync("/api/MealPlans/generate",new { startDate=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) });
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var plan=(await response.Content.ReadFromJsonAsync<MealPlanDto>())!;
        Assert.Equal(7,plan.Menu!.Days.Count);
        Assert.All(plan.Menu.Days,day=>
        {
            Assert.Equal(plan.StartDate!.Value.AddDays(day.DayIndex-1),day.Date);
            Assert.Equal((byte)(((int)day.Date.DayOfWeek+6)%7+1),day.DayOfWeek);
        });
        Assert.Equal(21,plan.Menu.Days.SelectMany(d=>d.Meals).Select(m=>m.MealId).Distinct().Count());
        await using var db=fixture.Context();
        Assert.Equal(21,await db.MealPlanSchedule.CountAsync(s=>s.MealPlanId==plan.Id));
        Assert.NotNull((await db.MealPlans.SingleAsync(p=>p.Id==plan.Id)).GenerationSnapshot);
        var slot=plan.Menu.Days[0].Meals[0];
        await db.Meals.Where(m=>m.Id==slot.MealId).ExecuteUpdateAsync(s=>s.SetProperty(m=>m.ComboName,"Changed later").SetProperty(m=>m.TotalCalories,1));
        var restored=(await client.GetFromJsonAsync<MealPlanDto>($"/api/MealPlans/{plan.Id}"))!;
        Assert.Equal(slot.Name,restored.Menu!.Days[0].Meals[0].Name);
        Assert.Equal(slot.Calories,restored.Menu.Days[0].Meals[0].Calories);
        using var otherClient=fixture.Factory.Client(other);
        Assert.Equal(HttpStatusCode.NotFound,(await otherClient.GetAsync($"/api/MealPlans/{plan.Id}")).StatusCode);
        var list=await otherClient.GetFromJsonAsync<VeganHelper.BLL.DTOs.PagedResult<MealPlanDto>>("/api/MealPlans");
        Assert.Empty(list!.Items);
        Assert.Equal(0,await db.BmiHistories.CountAsync(h=>h.UserId==owner));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Generate_UnknownAllergyOrInsufficientMealsReturnsConflictWithoutPlan(bool unknownAllergy)
    {
        var owner=await SeedAsync(unknownAllergy);
        using var client=fixture.Factory.Client(owner);
        if(unknownAllergy)
        {
            var allergyResponse=await client.PutAsJsonAsync("/api/HealthProfile/allergies",new { allergyIngredientIds=Array.Empty<long>(),customAllergies=new[]{"Unresolved alias " + Guid.NewGuid().ToString("N")} });
            Assert.Equal(HttpStatusCode.OK,allergyResponse.StatusCode);
        }
        var response=await client.PostAsJsonAsync("/api/MealPlans/generate",new { startDate=DateOnly.FromDateTime(DateTime.UtcNow) });
        Assert.Equal(HttpStatusCode.Conflict,response.StatusCode);
        await using var db=fixture.Context();
        Assert.False(await db.MealPlans.AnyAsync(p=>p.UserId==owner));
    }

    [Fact]
    public async Task Generate_InvalidDateOrUnder20IsBadRequestWithoutWrites()
    {
        var owner=await SeedAsync(); using var client=fixture.Factory.Client(owner);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/MealPlans/generate",new { startDate="0001-01-01" })).StatusCode);
        await using var db=fixture.Context();
        await db.UserProfiles.Where(p=>p.UserId==owner).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.BirthDate,new DateOnly(2010,1,1)));
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/MealPlans/generate",new { startDate=DateOnly.FromDateTime(DateTime.UtcNow) })).StatusCode);
        Assert.False(await db.MealPlans.AnyAsync(p=>p.UserId==owner));
    }

    [Fact]
    public async Task UnknownCustomAlias_ReselectedViaIngredientPickerStillBlocksGeneration()
    {
        var owner=await SeedAsync(); using var client=fixture.Factory.Client(owner);
        var alias="arachide "+Guid.NewGuid().ToString("N");
        Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync("/api/HealthProfile/allergies",new
        { allergyIngredientIds=Array.Empty<long>(),customAllergies=new[]{alias} })).StatusCode);
        await using var db=fixture.Context();
        var aliasId=await db.UserAllergies.Where(a=>a.UserId==owner).Select(a=>a.IngredientId).SingleAsync();
        Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync("/api/HealthProfile/allergies",new
        { allergyIngredientIds=new[]{aliasId},customAllergies=Array.Empty<string>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/MealPlans/generate",new
        { startDate=DateOnly.FromDateTime(DateTime.UtcNow) })).StatusCode);
        Assert.False(await db.MealPlans.AnyAsync(p=>p.UserId==owner));
    }

    [Fact]
    public async Task Repository_FailedScheduleInsertRollsBackPlan()
    {
        var userId=await SeedAsync(false);
        await using var db=fixture.Context();
        var plan=new MealPlan { UserId=userId,StartDate=new(2026,10,12),EndDate=new(2026,10,18),DietType="vegan",
            GenerationSource="manual",Status="completed",AllergiesSnapshot="[]",AvailableIngredientsSnapshot="[]",CreatedAt=DateTime.UtcNow };
        await Assert.ThrowsAsync<DbUpdateException>(()=>new MealPlanningRepository(db).SaveAsync(plan,
            [new MealPlanSchedule { DayOfWeek=1,MealId=-1,MealTime="breakfast",PortionMultiplier=1 }],default));
        await using var fresh=fixture.Context();
        Assert.False(await fresh.MealPlans.AnyAsync(p=>p.UserId==userId));
    }
}
