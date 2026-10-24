using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VeganHelper.BLL.DTOs.MealPlanning;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class MealPlanningApiTests(Member2ApiFixture fixture)
{
    private async Task<long> SeedAsync(bool catalog=true, bool grantAccess=true)
    {
        await using var db=fixture.Context();
        var key=Guid.NewGuid().ToString("N");
        var user=new User { Username="plan_"+key,Email=key+"@example.invalid",RoleId=1,CreatedAt=DateTime.UtcNow };
        db.Users.Add(user); await db.SaveChangesAsync();
        db.UserProfiles.Add(new UserProfile
        { UserId=user.Id,DisplayName="Plan test",HeightCm=165,WeightKg=60,BirthDate=new(1996,1,1),
            BiologicalSex="female",ActivityLevel="moderate",DietType="vegan" });
        if (grantAccess)
        {
            var now = DateTime.UtcNow;
            var order = new MealPlanPaymentOrder
            {
                UserId = user.Id, MealPlanPackageId = 1, PackageCodeSnapshot = "monthly",
                PackageNameSnapshot = "1 month", DurationMonthsSnapshot = 1, AmountVnd = 100_000,
                Status = "paid", CreatedAt = now, PaidAt = now
            };
            db.MealPlanPaymentOrders.Add(order);
            await db.SaveChangesAsync();
            db.UserMealPlanAccesses.Add(new UserMealPlanAccess
            {
                UserId = user.Id, MealPlanPackageId = 1, PaymentOrderId = order.Id,
                StartsAt = now.AddDays(-1), ExpiresAt = now.AddMonths(1), CreatedAt = now
            });
        }
        if(catalog)
        {
            var ingredient=new Ingredient { Name="Rice " + key,DefaultUnit="g",CaloriesPer100g=130 };
            db.Ingredients.Add(ingredient); await db.SaveChangesAsync();
            for(var n=0;n<35;n++)
            {
                var meal=new Meal { OwnerUserId=user.Id,ComboName=$"Test {key} {n}",ImageUrl=$"https://media.example/{n}.jpg",DietType="vegan",Servings=1,
                    TotalCalories=n<10?550:700,CreatedAt=DateTime.UtcNow };
                db.Meals.Add(meal); await db.SaveChangesAsync();
                db.MealSuitableTimes.AddRange((n<10?new[]{"breakfast"}:new[]{"lunch","dinner"})
                    .Select(time=>new MealSuitableTime { MealId=meal.Id,MealTime=time }));
                db.MealIngredients.Add(new MealIngredient { MealId=meal.Id,IngredientId=ingredient.Id,CalculatedQuantity=300,Unit="g" });
            }
        }
        await db.SaveChangesAsync(); return user.Id;
    }

    private async Task<MealPlanDto> GoalAsync(long owner)
    {
        using var client=fixture.Factory.Client(owner);
        var today=DateOnly.FromDateTime(DateTime.UtcNow);
        var response=await client.PostAsJsonAsync("/api/MealPlans/generate",new {startDate=today,mode="weight_goal",
            targetWeightKg=58,targetDate=today.AddDays(84),eligibilityConfirmed=true,timeZoneId="UTC"});
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MealPlanDto>())!;
    }

    [Fact]
    public async Task Trial_GoalRevisionsSaveTrackingAndRespectSharedGenerationLimit()
    {
        var owner=await SeedAsync(grantAccess:false);
        var initial=await GoalAsync(owner);
        using var client=fixture.Factory.Client(owner);
        var previewResponse=await client.PostAsJsonAsync($"/api/MealPlans/{initial.Id}/revision-preview",
            new {expectedVersion=0,profileFingerprint="",eligibilityConfirmed=true,nextWeek=true});
        Assert.Equal(HttpStatusCode.OK,previewResponse.StatusCode);
        var preview=(await previewResponse.Content.ReadFromJsonAsync<RevisionPreview>())!;
        Assert.True(preview.Allowed);
        var response=await client.PostAsJsonAsync($"/api/MealPlans/{initial.Id}/revisions",
            new RevisionRequest(preview.Version,preview.ProfileFingerprint,true,NextWeek:true));
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var revision=(await response.Content.ReadFromJsonAsync<MealPlanDto>())!;
        Assert.Equal(21,revision.Routine!.Entries.Count);
        Assert.Equal(initial.Id,revision.Routine.PreviousPlanId);
        await using(var db=fixture.Context())
            Assert.Equal(2,await db.MealPlans.CountAsync(p=>p.UserId==owner && p.IsTrialGeneration));
        await GoalAsync(owner);
        var blocked=await client.PostAsJsonAsync($"/api/MealPlans/{revision.Id}/revision-preview",
            new {expectedVersion=0,profileFingerprint="",eligibilityConfirmed=true,nextWeek=true});
        Assert.Equal(HttpStatusCode.PaymentRequired,blocked.StatusCode);
        var bypass=await client.PostAsJsonAsync($"/api/MealPlans/{revision.Id}/revisions",
            new RevisionRequest(0,"",true,NextWeek:true));
        Assert.Equal(HttpStatusCode.PaymentRequired,bypass.StatusCode);
        var access=await client.GetFromJsonAsync<MealPlanAccessDto>("/api/meal-plan-payments/access");
        Assert.False(access!.CanGenerate);
        Assert.Equal(0,access.TrialGenerationsRemaining);
        await using var verify=fixture.Context();
        Assert.Equal(3,await verify.MealPlans.CountAsync(p=>p.UserId==owner && p.IsTrialGeneration));
    }
    [Fact]
    public async Task Goal_TracksOnlyOwnedElapsedMealsAndRejectsStaleVersions()
    {
        var owner=await SeedAsync();var other=await SeedAsync(false);var plan=await GoalAsync(owner);
        Assert.Equal("weight_goal",plan.Menu!.Goal);Assert.NotNull(plan.Routine);
        await using var db=fixture.Context();
        var entry=await db.Set<MealPlanEntry>().Where(e=>e.MealPlanId==plan.Id).OrderBy(e=>e.Id).FirstAsync();
        entry.DueAt=DateTime.UtcNow.AddMinutes(-1);await db.SaveChangesAsync();
        using var client=fixture.Factory.Client(owner);using var stranger=fixture.Factory.Client(other);
        var log=new {date=entry.Date,mealTime=entry.MealTime,status="changed",expectedVersion=0};
        Assert.Equal(HttpStatusCode.NotFound,(await stranger.PutAsJsonAsync($"/api/MealPlans/{plan.Id}/tracking",log)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync($"/api/MealPlans/{plan.Id}/tracking",log)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync($"/api/MealPlans/{plan.Id}/tracking",log)).StatusCode);
        var future=plan.Menu.Days[6];
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync($"/api/MealPlans/{plan.Id}/tracking",new
        {date=future.Date,mealTime="breakfast",status="followed",expectedVersion=1})).StatusCode);
    }

    [Fact]
    public async Task Revision_RechecksSafetyAndProfileAndPreservesHistoryUnderConcurrency()
    {
        var owner=await SeedAsync();var plan=await GoalAsync(owner);
        await using(var db=fixture.Context())
        {
            var past=await db.Set<MealPlanEntry>().SingleAsync(e=>e.MealPlanId==plan.Id && e.Date==plan.StartDate && e.MealTime=="breakfast");
            past.Status="changed";past.Note="Recorded before revision";past.LoggedAt=DateTime.UtcNow;
            var storedPlan=await db.MealPlans.SingleAsync(p=>p.Id==plan.Id);
            var jsonOptions=new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var storedMenu=JsonSerializer.Deserialize<WeeklyMenuDto>(storedPlan.GenerationSnapshot!,jsonOptions)!;
            foreach(var meal in storedMenu.Days.SelectMany(day=>day.Meals))
            {
                meal.ImageUrl=null;
                foreach(var ingredient in meal.Ingredients) { ingredient.Quantity=61.65m;ingredient.Unit="g"; }
            }
            storedPlan.GenerationSnapshot=JsonSerializer.Serialize(storedMenu,jsonOptions);
            await db.SaveChangesAsync();
        }
        using var client=fixture.Factory.Client(owner);
        async Task<RevisionPreview> Preview()=> (await (await client.PostAsJsonAsync($"/api/MealPlans/{plan.Id}/revision-preview",
            new {expectedVersion=0,profileFingerprint="",eligibilityConfirmed=true})).Content.ReadFromJsonAsync<RevisionPreview>())!;
        var preview=await Preview();Assert.True(preview.Allowed);
        await using(var db=fixture.Context())
            await db.UserProfiles.Where(p=>p.UserId==owner).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.WeightKg,61));
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"/api/MealPlans/{plan.Id}/revisions",
            new RevisionRequest(preview.Version,preview.ProfileFingerprint,true))).StatusCode);
        var unsafeResponse=await client.PostAsJsonAsync($"/api/MealPlans/{plan.Id}/revision-preview",new
        {expectedVersion=0,profileFingerprint="",eligibilityConfirmed=true,targetDate=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(8)});
        Assert.False((await unsafeResponse.Content.ReadFromJsonAsync<RevisionPreview>())!.Allowed);
        preview=await Preview();Assert.True(preview.Allowed);
        var request=new RevisionRequest(preview.Version,preview.ProfileFingerprint,true);
        using var second=fixture.Factory.Client(owner);
        var results=await Task.WhenAll(client.PostAsJsonAsync($"/api/MealPlans/{plan.Id}/revisions",request),
            second.PostAsJsonAsync($"/api/MealPlans/{plan.Id}/revisions",request));
        Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Created);
        Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Conflict);
        var revision=(await results.Single(r=>r.StatusCode==HttpStatusCode.Created).Content.ReadFromJsonAsync<MealPlanDto>())!;
        Assert.All(revision.Menu!.Days[0].Meals,meal=>Assert.StartsWith("https://media.example/",meal.ImageUrl));
        Assert.All(revision.Menu.Days[0].Meals.SelectMany(meal=>meal.Ingredients),ingredient=>Assert.Equal(60m,ingredient.Quantity));
        Assert.Equal(plan.Menu!.Days[0].Meals.Select(m=>m.MealId),revision.Menu!.Days[0].Meals.Select(m=>m.MealId));
        Assert.Equal(plan.Id,revision.Routine!.PreviousPlanId);
        var copied=revision.Routine.Entries.Single(e=>e.Date==plan.StartDate && e.MealTime=="breakfast");
        Assert.Equal("changed",copied.Status);Assert.Equal("Recorded before revision",copied.Note);
        var old=await client.GetFromJsonAsync<MealPlanDto>($"/api/MealPlans/{plan.Id}");
        Assert.Equal("superseded",old!.Routine!.State);
        Assert.Equal(plan.Menu.Days[0].TotalCalories,old.Menu!.Days[0].TotalCalories);
    }

    [Fact]
    public async Task ThreeDayReview_CrossesWeeksAndDoesNotDuplicateRecommendations()
    {
        var owner=await SeedAsync(false);var today=DateOnly.FromDateTime(DateTime.UtcNow);
        await using(var db=fixture.Context())
        {
            MealPlan Make(DateOnly start)=>new() {UserId=owner,StartDate=start,EndDate=start.AddDays(6),DietType="vegan",
                GenerationSource="manual",Status="completed",AllergiesSnapshot="[]",AvailableIngredientsSnapshot="[]",CreatedAt=DateTime.UtcNow};
            var first=Make(today.AddDays(-10));var second=Make(today.AddDays(-3));
            db.MealPlans.AddRange(first,second);await db.SaveChangesAsync();
            db.AddRange(new MealPlanRoutine {MealPlanId=first.Id,UserId=owner,RootPlanId=first.Id,Mode="weight_goal",State="continued",TargetWeightKg=58,TargetDate=today.AddDays(80),EligibilityConfirmed=true,CreatedAt=DateTime.UtcNow},
                new MealPlanRoutine {MealPlanId=second.Id,UserId=owner,RootPlanId=first.Id,PreviousPlanId=first.Id,Mode="weight_goal",TargetWeightKg=58,TargetDate=today.AddDays(80),EligibilityConfirmed=true,CreatedAt=DateTime.UtcNow});
            foreach(var plan in new[]{first,second})
            for(var i=0;i<7;i++)
            foreach(var slot in new[]{"breakfast","lunch","dinner"})
            {
                var date=plan.StartDate!.Value.AddDays(i);
                var changed=date>=today.AddDays(-4) && date<=today.AddDays(-3);
                db.Add(new MealPlanEntry {MealPlanId=plan.Id,Date=date,MealTime=slot,Status=changed?"changed":"unlogged",
                    LoggedAt=changed?DateTime.UtcNow:null,DueAt=DateTime.SpecifyKind(date.ToDateTime(new(12,0)),DateTimeKind.Utc)});
            }
            await db.SaveChangesAsync();
        }
        for(var i=0;i<2;i++)
        {
            using var scope=fixture.Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<VeganHelper.BLL.Services.MealPlanning.MealRoutineReview>().RunAsync(default);
        }
        await using var check=fixture.Context();
        Assert.Equal(1,await check.Set<Notification>().CountAsync(n=>n.UserId==owner && n.EventKey.StartsWith("meal-review:")));
    }

    [Fact]
    public async Task RoutineTables_BlockBrowserRoles()
    {
        await using var db=fixture.Context();
        foreach(var table in new[]{"meal_plan_routines","meal_plan_entries"})
        {
            var readable=await db.Database.SqlQuery<bool>($"SELECT has_table_privilege('anon',{table},'SELECT') AS \"Value\"").SingleAsync();
            Assert.False(readable);
            var writable=await db.Database.SqlQuery<bool>($"SELECT has_table_privilege('authenticated',{table},'INSERT') AS \"Value\"").SingleAsync();
            Assert.False(writable);
        }
    }

    [Fact]
    public async Task ReminderSweep_IsIdempotentAndUnloggedMealsNeverTriggerReview()
    {
        var owner=await SeedAsync();var plan=await GoalAsync(owner);
        await using(var db=fixture.Context())
        {
            var r=await db.Set<MealPlanRoutine>().SingleAsync(r=>r.MealPlanId==plan.Id);r.RemindersEnabled=true;
            var entry=await db.Set<MealPlanEntry>().Where(e=>e.MealPlanId==plan.Id).OrderBy(e=>e.Id).FirstAsync();
            entry.DueAt=DateTime.UtcNow.AddMinutes(-1);await db.SaveChangesAsync();
        }
        async Task Sweep()
        {
            using var scope=fixture.Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<VeganHelper.BLL.Services.MealPlanning.MealRoutineReview>().RunAsync(default);
        }
        await Sweep();await Sweep();
        await using var fresh=fixture.Context();
        Assert.Equal(1,await fresh.Set<Notification>().CountAsync(n=>n.UserId==owner && n.EventKey.StartsWith("meal:")));
        Assert.Equal(0,await fresh.Set<Notification>().CountAsync(n=>n.UserId==owner && n.EventKey.StartsWith("meal-review:")));
    }

    [Fact]
    public async Task Goal_NextWeekUsesFreshSafetyAndCanDisableReminders()
    {
        var owner=await SeedAsync();var plan=await GoalAsync(owner);using var client=fixture.Factory.Client(owner);
        var response=await client.PutAsJsonAsync($"/api/MealPlans/{plan.Id}/reminders",new
        {enabled=false,timeZoneId="UTC",breakfastTime="08:00:00",lunchTime="12:30:00",dinnerTime="19:00:00",expectedVersion=0});
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var routine=(await response.Content.ReadFromJsonAsync<MealRoutineDto>())!;Assert.False(routine.RemindersEnabled);
        var preview=(await (await client.PostAsJsonAsync($"/api/MealPlans/{plan.Id}/revision-preview",new
        {expectedVersion=routine.Version,profileFingerprint="",eligibilityConfirmed=true,nextWeek=true})).Content.ReadFromJsonAsync<RevisionPreview>())!;
        Assert.True(preview.Allowed);
        var nextResponse=await client.PostAsJsonAsync($"/api/MealPlans/{plan.Id}/revisions",
            new RevisionRequest(preview.Version,preview.ProfileFingerprint,true,NextWeek:true));
        Assert.Equal(HttpStatusCode.Created,nextResponse.StatusCode);
        var next=(await nextResponse.Content.ReadFromJsonAsync<MealPlanDto>())!;
        Assert.Equal(plan.EndDate!.Value.AddDays(1),next.StartDate);
        Assert.Equal(21,next.Routine!.Entries.Count);
        var previous=await client.GetFromJsonAsync<MealPlanDto>($"/api/MealPlans/{plan.Id}");
        Assert.Equal("continued",previous!.Routine!.State);
        await using var db=fixture.Context();
        var slot=await db.Set<MealPlanEntry>().Where(e=>e.MealPlanId==plan.Id).OrderBy(e=>e.Id).FirstAsync();
        slot.DueAt=DateTime.UtcNow.AddMinutes(-1);await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync($"/api/MealPlans/{plan.Id}/tracking",new
        {date=slot.Date,mealTime=slot.MealTime,status="followed",expectedVersion=previous.Routine.Version})).StatusCode);
    }

    [Fact]
    public async Task Generate_GuestIsUnauthorized()
    {
        using var client=fixture.Factory.Client();
        var response=await client.PostAsJsonAsync("/api/MealPlans/generate",new { startDate=DateOnly.FromDateTime(DateTime.UtcNow) });
        Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
    }

    [Fact]
    public async Task GoalPreview_ScreensUnsafeTargetsWithoutSaving()
    {
        var owner=await SeedAsync(false);
        using var client=fixture.Factory.Client(owner);
        var response=await client.PostAsJsonAsync("/api/MealPlans/goal-preview",new
        { startDate=DateOnly.FromDateTime(DateTime.UtcNow),targetWeightKg=40,targetDate=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),eligibilityConfirmed=true });
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        using var json=System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("allowed").GetBoolean());
        await using var db=fixture.Context();
        Assert.False(await db.MealPlans.AnyAsync(p=>p.UserId==owner));
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
        var storedPlan=await db.MealPlans.SingleAsync(p=>p.Id==plan.Id);
        Assert.NotNull(storedPlan.GenerationSnapshot);
        Assert.False(storedPlan.IsTrialGeneration);
        var paidAccess=await client.GetFromJsonAsync<MealPlanAccessDto>("/api/meal-plan-payments/access");
        Assert.Equal(3,paidAccess!.TrialGenerationsRemaining);
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

    [Fact]
    public async Task Trial_AllowsThreeSuccessfulGenerationsThenAllowsReadingOnlyTrialPlans()
    {
        var owner=await SeedAsync(catalog:true,grantAccess:false);
        using var client=fixture.Factory.Client(owner);
        var today=DateOnly.FromDateTime(DateTime.UtcNow);

        for(var attempt=0;attempt<3;attempt++)
        {
            var response=await client.PostAsJsonAsync("/api/MealPlans/generate",new { startDate=today.AddDays(attempt) });
            Assert.Equal(HttpStatusCode.Created,response.StatusCode);
            var plan=(await response.Content.ReadFromJsonAsync<MealPlanDto>())!;
            await using var db=fixture.Context();
            Assert.True((await db.MealPlans.SingleAsync(p=>p.Id==plan.Id)).IsTrialGeneration);
        }

        var blocked=await client.PostAsJsonAsync("/api/MealPlans/generate",new { startDate=today.AddDays(3) });
        Assert.Equal(HttpStatusCode.PaymentRequired,blocked.StatusCode);
        var access=await client.GetFromJsonAsync<MealPlanAccessDto>("/api/meal-plan-payments/access");
        var page=await client.GetFromJsonAsync<VeganHelper.BLL.DTOs.PagedResult<MealPlanDto>>("/api/MealPlans");

        Assert.False(access!.CanGenerate);
        Assert.Equal(0,access.TrialGenerationsRemaining);
        Assert.True(access.CanViewTrialPlans);
        Assert.False(access.CanViewExistingPlans);
        Assert.Equal(3,page!.TotalCount);
    }

    [Fact]
    public async Task Trial_ConcurrentRequestsCannotExceedThreeSuccessfulGenerations()
    {
        var owner=await SeedAsync(catalog:true,grantAccess:false);
        var now=DateTime.UtcNow;
        await using(var db=fixture.Context())
        {
            db.MealPlans.AddRange(Enumerable.Range(0,2).Select(index=>new MealPlan
            {
                UserId=owner,IsTemplate=false,StartDate=DateOnly.FromDateTime(now).AddDays(index),
                EndDate=DateOnly.FromDateTime(now).AddDays(index+6),DietType="vegan",AllergiesSnapshot="[]",
                AvailableIngredientsSnapshot="[]",GenerationSource="manual",Status="completed",
                IsTrialGeneration=true,CreatedAt=now.AddMinutes(-index)
            }));
            await db.SaveChangesAsync();
        }

        using var client=fixture.Factory.Client(owner);
        var start=DateOnly.FromDateTime(now).AddDays(1);
        var requests=Enumerable.Range(0,4).Select(index=>client.PostAsJsonAsync("/api/MealPlans/generate",new
            { startDate=start.AddDays(index) }));
        var responses=await Task.WhenAll(requests);

        Assert.Single(responses,response=>response.StatusCode==HttpStatusCode.Created);
        Assert.Equal(3,responses.Count(response=>response.StatusCode==HttpStatusCode.PaymentRequired));
        await using var verify=fixture.Context();
        Assert.Equal(3,await verify.MealPlans.CountAsync(p=>p.UserId==owner && p.IsTrialGeneration));
    }

    [Fact]
    public async Task AfterPaidGrace_OnlyTrialPlansRemainReadable()
    {
        var owner=await SeedAsync(catalog:false,grantAccess:true);
        var now=DateTime.UtcNow;
        long paidPlanId;
        long trialPlanId;
        await using(var db=fixture.Context())
        {
            var paidPlan=new MealPlan
            {
                UserId=owner,IsTemplate=false,StartDate=DateOnly.FromDateTime(now),EndDate=DateOnly.FromDateTime(now).AddDays(6),
                DietType="vegan",AllergiesSnapshot="[]",AvailableIngredientsSnapshot="[]",GenerationSource="manual",
                Status="completed",CreatedAt=now.AddDays(-2)
            };
            var trialPlan=new MealPlan
            {
                UserId=owner,IsTemplate=false,StartDate=DateOnly.FromDateTime(now).AddDays(1),EndDate=DateOnly.FromDateTime(now).AddDays(7),
                DietType="vegan",AllergiesSnapshot="[]",AvailableIngredientsSnapshot="[]",GenerationSource="manual",
                Status="completed",IsTrialGeneration=true,CreatedAt=now.AddDays(-1)
            };
            db.MealPlans.AddRange(paidPlan,trialPlan);
            await db.SaveChangesAsync();
            paidPlanId=paidPlan.Id;
            trialPlanId=trialPlan.Id;
            await db.UserMealPlanAccesses.Where(a=>a.UserId==owner)
                .ExecuteUpdateAsync(setters=>setters.SetProperty(a=>a.StartsAt,now.AddMonths(-1)).SetProperty(a=>a.ExpiresAt,now.AddDays(-8)));
        }

        using var client=fixture.Factory.Client(owner);
        var access=await client.GetFromJsonAsync<MealPlanAccessDto>("/api/meal-plan-payments/access");
        var plans=await client.GetFromJsonAsync<VeganHelper.BLL.DTOs.PagedResult<MealPlanDto>>("/api/MealPlans");

        Assert.False(access!.CanViewExistingPlans);
        Assert.True(access.CanViewTrialPlans);
        Assert.Single(plans!.Items);
        Assert.Equal(trialPlanId,plans.Items.Single().Id);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/MealPlans/{paidPlanId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync($"/api/MealPlans/{trialPlanId}")).StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Generate_UnknownAllergyOrInsufficientMealsReturnsConflictWithoutPlan(bool unknownAllergy)
    {
        var owner=await SeedAsync(unknownAllergy,grantAccess:false);
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
        var access=await client.GetFromJsonAsync<MealPlanAccessDto>("/api/meal-plan-payments/access");
        Assert.Equal(3,access!.TrialGenerationsRemaining);
    }

    [Fact]
    public async Task Generate_InvalidDateOrUnder20IsBadRequestWithoutWrites()
    {
        var owner=await SeedAsync(grantAccess:false); using var client=fixture.Factory.Client(owner);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/MealPlans/generate",new { startDate="0001-01-01" })).StatusCode);
        await using var db=fixture.Context();
        await db.UserProfiles.Where(p=>p.UserId==owner).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.BirthDate,new DateOnly(2010,1,1)));
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/MealPlans/generate",new { startDate=DateOnly.FromDateTime(DateTime.UtcNow) })).StatusCode);
        Assert.False(await db.MealPlans.AnyAsync(p=>p.UserId==owner));
        var access=await client.GetFromJsonAsync<MealPlanAccessDto>("/api/meal-plan-payments/access");
        Assert.Equal(3,access!.TrialGenerationsRemaining);
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
