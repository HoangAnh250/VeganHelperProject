using System.Data;
using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class MealPlanningRepository(AppDbContext db) : IMealPlanningRepository
{
    public async Task<MealPlanningInput> LoadAsync(long userId, CancellationToken ct)
    {
        // A consistent input snapshot; persistence stores exactly this version of the inputs/output.
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
        var input=new MealPlanningInput
        {
            Profile=await db.UserProfiles.AsNoTracking().SingleOrDefaultAsync(p=>p.UserId==userId,ct),
            Ingredients=await db.Ingredients.AsNoTracking().OrderBy(i=>i.Id).ToListAsync(ct),
            Allergies=await (from a in db.UserAllergies.AsNoTracking()
                join i in db.Ingredients on a.IngredientId equals i.Id
                where a.UserId==userId orderby i.Id
                select new AllergyProjection { IngredientId=i.Id, Name=i.Name, IsCustom=a.IsCustom }).ToListAsync(ct),
            Meals=await db.Meals.AsNoTracking().Where(m=>m.OwnerUserId==null || m.OwnerUserId==userId)
                .OrderBy(m=>m.Id).Select(m=>new MealCandidate
                {
                    Id=m.Id, Name=m.ComboName, DietType=m.DietType, Servings=m.Servings, TotalCalories=m.TotalCalories
                }).ToListAsync(ct)
        };
        var ids=input.Meals.Select(m=>m.Id).ToArray();
        var times=await db.MealSuitableTimes.AsNoTracking().Where(t=>ids.Contains(t.MealId)).ToListAsync(ct);
        var quantities=await (from mi in db.MealIngredients.AsNoTracking()
            join i in db.Ingredients on mi.IngredientId equals i.Id
            where ids.Contains(mi.MealId)
            select new { mi.MealId, Ingredient=new RecipeIngredient
            { IngredientId=i.Id, Name=i.Name, Quantity=mi.CalculatedQuantity, Unit=mi.Unit } }).ToListAsync(ct);
        var timeLookup=times.ToLookup(t=>t.MealId);
        var ingredientLookup=quantities.ToLookup(i=>i.MealId);
        foreach(var meal in input.Meals)
        {
            meal.SuitableTimes=timeLookup[meal.Id].Select(t=>t.MealTime).ToList();
            meal.Ingredients=ingredientLookup[meal.Id].Select(i=>i.Ingredient).OrderBy(i=>i.IngredientId).ToList();
        }
        await tx.CommitAsync(ct);
        return input;
    }

    public async Task SaveAsync(MealPlan plan,IReadOnlyList<MealPlanSchedule> schedule,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        db.MealPlans.Add(plan);
        await db.SaveChangesAsync(ct);
        foreach(var slot in schedule) slot.MealPlanId=plan.Id;
        db.MealPlanSchedule.AddRange(schedule);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<bool> TrySaveTrialAsync(
        MealPlan plan,
        IReadOnlyList<MealPlanSchedule> schedule,
        int generationLimit,
        CancellationToken ct)
    {
        if (plan.UserId is not long userId || userId <= 0)
            throw new ArgumentException("A trial meal plan must belong to a user.", nameof(plan));
        if (generationLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(generationLimit));

        await using var tx=await db.Database.BeginTransactionAsync(ct);

        // Serialize quota checks per user so concurrent requests cannot exceed the limit.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM public.users WHERE id = {userId} FOR UPDATE", ct);

        var used=await db.MealPlans.CountAsync(p=>p.UserId==userId && p.IsTrialGeneration,ct);
        if(used>=generationLimit)
        {
            await tx.CommitAsync(ct);
            return false;
        }

        plan.IsTrialGeneration=true;
        db.MealPlans.Add(plan);
        await db.SaveChangesAsync(ct);
        foreach(var slot in schedule) slot.MealPlanId=plan.Id;
        db.MealPlanSchedule.AddRange(schedule);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public Task<int> CountTrialGenerationsAsync(long userId,CancellationToken ct)=>
        db.MealPlans.CountAsync(p=>p.UserId==userId && p.IsTrialGeneration,ct);

    public Task<MealPlan?> GetAsync(long userId,long id,bool trialOnly,CancellationToken ct)
    {
        var query=db.MealPlans.AsNoTracking().Where(p=>p.Id==id && p.UserId==userId && !p.IsTemplate);
        if(trialOnly) query=query.Where(p=>p.IsTrialGeneration);
        return query.SingleOrDefaultAsync(ct);
    }

    public async Task<DatabasePage<MealPlan>> ListAsync(long userId,int pageIndex,int pageSize,bool trialOnly,CancellationToken ct)
    {
        var query=db.MealPlans.AsNoTracking().Where(p=>p.UserId==userId && !p.IsTemplate);
        if(trialOnly) query=query.Where(p=>p.IsTrialGeneration);
        var count=await query.LongCountAsync(ct);
        var items=await query.OrderByDescending(p=>p.CreatedAt).ThenByDescending(p=>p.Id)
            .Skip((pageIndex-1)*pageSize).Take(pageSize).ToListAsync(ct);
        return new(count,items);
    }
}
