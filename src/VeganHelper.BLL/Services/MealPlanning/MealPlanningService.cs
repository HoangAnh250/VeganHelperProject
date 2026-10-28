using System.Text.Json;
using AutoMapper;
using FluentValidation;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.MealPlanning;
using VeganHelper.BLL.Exceptions;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services.MealPlanning;

public interface IMealPlanningService
{
    Task<MealPlanDto> GenerateAsync(long userId,GenerateMealPlanRequest request,CancellationToken ct);
    Task<MealPlanDto> RegenerateAsync(long userId,long id,CancellationToken ct);
    Task ArchiveAsync(long userId,long id,CancellationToken ct);
    Task<MealPlanDto> GetAsync(long userId,long id,CancellationToken ct);
    Task<MealPlanDto> RenameAsync(long userId,long id,UpdateMealPlanNameRequest request,CancellationToken ct);
    Task<PagedResult<MealPlanDto>> ListAsync(long userId,int pageIndex,int pageSize,CancellationToken ct);
}

public sealed class MealPlanningService(IMealPlanningRepository repository,IMapper mapper,
    IValidator<GenerateMealPlanRequest> validator,TimeProvider clock,MealRoutineService routines,
    IMealRoutineRepository routineRepository,IMealPlanPaymentService payments) : IMealPlanningService
{
    private static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web);

    public async Task<MealPlanDto> GenerateAsync(long userId,GenerateMealPlanRequest request,CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request,ct);
        var planName=MealPlanNaming.NormalizeOrDefault(request.Name,request.StartDate);
        var access=await payments.GetAccessAsync(userId,ct);
        if(!access.CanGenerate)
            throw new MealPlanAccessRequiredException("Your three free meal-plan trials have been used. Purchase a package to generate more plans.","MEAL_PLAN_TRIAL_LIMIT_REACHED");
        MealRoutineService.ValidateSettings(request);
        var today=MealRoutineService.LocalToday(clock,request.TimeZoneId);
        if(request.StartDate<today || request.StartDate>today.AddYears(1))
            throw new ArgumentException("Start date must be today or within the next year in your selected timezone.");
        await using var tx=await routineRepository.BeginAsync(ct);
        await routineRepository.LockProfileAsync(userId,ct);
        var endDate=request.StartDate.AddDays(6);
        if(await repository.HasOverlappingPlanAsync(userId,request.StartDate,endDate,null,ct))
            throw new ConflictException("You already have a weekly menu covering some of these dates. Delete or regenerate that menu before creating another.");
        var input=await repository.LoadAsync(userId,ct);
        GoalSafetyResult? safety=null;
        if(request.Mode=="weight_goal")
        {
            safety=WeightGoalSafety.Evaluate(input.Profile,request.StartDate,request.TargetWeightKg ?? 0,
                request.TargetDate ?? default,request.EligibilityConfirmed);
            if(!safety.Allowed) throw new ArgumentException(string.Join(" ",safety.Reasons));
        }
        var menu=MealPlanGenerator.Generate(input,today,new Random(Random.Shared.Next()),ct,
            safety?.TargetCalories,request.MealCalorieSplit);
        if(safety is not null)
        {
            WeightGoalSafety.ValidateGeneratedDays(menu.Days,safety,input.Profile!.BiologicalSex!);
            menu.Goal="weight_goal"; menu.Notes=MealRoutineService.GoalNotes();
        }
        foreach(var day in menu.Days)
        {
            day.Date=request.StartDate.AddDays(day.DayIndex-1);
            day.DayOfWeek=(byte)(((int)day.Date.DayOfWeek+6)%7+1);
        }
        var now=clock.GetUtcNow().UtcDateTime;
        var plan=new MealPlan
        {
            UserId=userId, IsTemplate=false, Name=planName, StartDate=request.StartDate, EndDate=request.StartDate.AddDays(6),
            HeightCm=input.Profile!.HeightCm, WeightKg=input.Profile.WeightKg, BmiValue=menu.Bmi, DietType=menu.DietType,
            AllergiesSnapshot=JsonSerializer.Serialize(input.Allergies,JsonOptions), AvailableIngredientsSnapshot="[]",
            GenerationSource="manual", IsTrialGeneration=access.Status!="active", GenerationSnapshot=JsonSerializer.Serialize(menu,JsonOptions),
            Status="completed", CreatedAt=now, UpdatedAt=now
        };
        var slots=menu.Days.SelectMany(day=>day.Meals.Select(meal=>new MealPlanSchedule
        { DayOfWeek=day.DayOfWeek, MealId=meal.MealId, MealTime=meal.MealTime, PortionMultiplier=meal.PortionMultiplier })).ToList();
        if(access.Status=="active")
        {
            await repository.SaveAsync(plan,slots,ct);
        }
        else if(!await repository.TrySaveTrialAsync(plan,slots,MealPlanAccessPolicy.TrialGenerationLimit,ct))
        {
            throw new MealPlanAccessRequiredException("Your three free meal-plan trials have been used. Purchase a package to generate more plans.","MEAL_PLAN_TRIAL_LIMIT_REACHED");
        }
        await routines.SaveRoutineAsync(plan,menu,request,ct);
        await tx.CommitAsync(ct);
        var dto=ToDto(plan); await routines.EnrichAsync(userId,dto,ct);
        return dto;
    }

    public async Task<MealPlanDto> RegenerateAsync(long userId,long id,CancellationToken ct)
    {
        var access=await EnsureCanViewAsync(userId,ct);
        await using var tx=await routineRepository.BeginAsync(ct);
        await routineRepository.LockProfileAsync(userId,ct);
        var plan=await repository.GetAsync(userId,id,!access.CanViewExistingPlans,ct)
            ?? throw new NotFoundException("Meal plan not found.");
        if(plan.StartDate is not DateOnly startDate || plan.EndDate is null)
            throw new ConflictException("This older menu cannot be regenerated. Create a new weekly menu instead.");

        var routine=await routineRepository.GetAsync(userId,id,true,ct)
            ?? throw new ConflictException("This menu has no saved regeneration settings. Create a new weekly menu instead.");
        if(routine.State=="superseded")
            throw new ConflictException("This menu has already been replaced by a later plan.");
        var today=MealRoutineService.LocalToday(clock,routine.TimeZoneId);
        if(startDate<today)
            throw new ConflictException("A weekly menu can only be regenerated before its start date.");
        var oldEntries=await routineRepository.EntriesAsync(id,ct);
        if(oldEntries.Any(entry=>entry.Status!="unlogged"))
            throw new ConflictException("This menu already has tracking activity and can no longer be regenerated.");

        var previousMenu=plan.GenerationSnapshot is null ? null : JsonSerializer.Deserialize<WeeklyMenuDto>(plan.GenerationSnapshot,JsonOptions);
        if(previousMenu is null) throw new ConflictException("This menu has no saved meal snapshot to regenerate.");
        var request=new GenerateMealPlanRequest
        {
            Name=plan.Name, StartDate=startDate, Mode=routine.Mode, TargetWeightKg=routine.TargetWeightKg,
            TargetDate=routine.TargetDate, EligibilityConfirmed=routine.EligibilityConfirmed,
            TimeZoneId=routine.TimeZoneId, RemindersEnabled=routine.RemindersEnabled,
            BreakfastTime=routine.BreakfastTime, LunchTime=routine.LunchTime, DinnerTime=routine.DinnerTime,
            MealCalorieSplit=previousMenu.MealCalorieSplit
        };
        await validator.ValidateAndThrowAsync(request,ct);
        MealRoutineService.ValidateSettings(request);

        var input=await repository.LoadAsync(userId,ct);
        GoalSafetyResult? safety=null;
        if(request.Mode=="weight_goal")
        {
            safety=WeightGoalSafety.Evaluate(input.Profile,startDate,request.TargetWeightKg ?? 0,
                request.TargetDate ?? default,request.EligibilityConfirmed);
            if(!safety.Allowed) throw new ConflictException("This weight goal is no longer safe with your current health profile. Update your goal or health profile before regenerating the menu. " + string.Join(" ",safety.Reasons));
        }
        var menu=MealPlanGenerator.Generate(input,today,new Random(Random.Shared.Next()),ct,
            safety?.TargetCalories,request.MealCalorieSplit);
        if(safety is not null)
        {
            WeightGoalSafety.ValidateGeneratedDays(menu.Days,safety,input.Profile!.BiologicalSex!);
            menu.Goal="weight_goal"; menu.Notes=MealRoutineService.GoalNotes();
        }
        foreach(var day in menu.Days)
        {
            day.Date=startDate.AddDays(day.DayIndex-1);
            day.DayOfWeek=(byte)(((int)day.Date.DayOfWeek+6)%7+1);
        }

        var now=clock.GetUtcNow().UtcDateTime;
        var replacement=new MealPlan
        {
            UserId=userId, IsTemplate=false, Name=plan.Name, StartDate=startDate, EndDate=plan.EndDate,
            HeightCm=input.Profile!.HeightCm, WeightKg=input.Profile.WeightKg, BmiValue=menu.Bmi, DietType=menu.DietType,
            AllergiesSnapshot=JsonSerializer.Serialize(input.Allergies,JsonOptions), AvailableIngredientsSnapshot="[]",
            GenerationSource="manual", IsTrialGeneration=plan.IsTrialGeneration, GenerationSnapshot=JsonSerializer.Serialize(menu,JsonOptions),
            Status="completed", UpdatedAt=now
        };
        var slots=menu.Days.SelectMany(day=>day.Meals.Select(meal=>new MealPlanSchedule
        { DayOfWeek=day.DayOfWeek, MealId=meal.MealId, MealTime=meal.MealTime, PortionMultiplier=meal.PortionMultiplier })).ToList();
        var updated=await repository.ReplaceGeneratedAsync(userId,id,replacement,slots,ct)
            ?? throw new NotFoundException("Meal plan not found.");
        routine.Version++;
        var newEntries=menu.Days.SelectMany(day=>day.Meals.Select(meal=>new MealPlanEntry
        {
            MealPlanId=id, Date=day.Date, MealTime=meal.MealTime, Status="unlogged",
            DueAt=MealRoutineService.Due(day.Date,meal.MealTime switch
            { "breakfast"=>routine.BreakfastTime, "lunch"=>routine.LunchTime, _=>routine.DinnerTime },routine.TimeZoneId)
        })).ToList();
        await routineRepository.ReplaceEntriesAsync(id,newEntries,ct);
        await routineRepository.SaveAsync(ct);
        await tx.CommitAsync(ct);
        var dto=ToDto(updated); await routines.EnrichAsync(userId,dto,ct);
        return dto;
    }

    public async Task ArchiveAsync(long userId,long id,CancellationToken ct)
    {
        var access=await EnsureCanViewAsync(userId,ct);
        await using var tx=await routineRepository.BeginAsync(ct);
        await routineRepository.LockProfileAsync(userId,ct);
        var routine=await routineRepository.GetAsync(userId,id,true,ct);
        if(routine is not null)
        {
            routine.State="superseded";
            routine.Version++;
        }
        _=await repository.ArchiveAsync(userId,id,!access.CanViewExistingPlans,clock.GetUtcNow().UtcDateTime,ct)
            ?? throw new NotFoundException("Meal plan not found.");
        await routineRepository.SaveAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<MealPlanDto> GetAsync(long userId,long id,CancellationToken ct)
    {
        var access=await EnsureCanViewAsync(userId,ct);
        var dto=ToDto(await repository.GetAsync(userId,id,!access.CanViewExistingPlans,ct) ?? throw new NotFoundException("Meal plan not found."));
        await routines.EnrichAsync(userId,dto,ct);
        return dto;
    }

    public async Task<MealPlanDto> RenameAsync(long userId,long id,UpdateMealPlanNameRequest request,CancellationToken ct)
    {
        var name=MealPlanNaming.NormalizeRequired(request?.Name);
        var access=await EnsureCanViewAsync(userId,ct);
        var plan=await repository.UpdateNameAsync(userId,id,name,!access.CanViewExistingPlans,
            clock.GetUtcNow().UtcDateTime,ct) ?? throw new NotFoundException("Meal plan not found.");
        var dto=ToDto(plan);
        await routines.EnrichAsync(userId,dto,ct);
        return dto;
    }

    public async Task<PagedResult<MealPlanDto>> ListAsync(long userId,int pageIndex,int pageSize,CancellationToken ct)
    {
        Pagination.Validate(pageIndex,pageSize);
        var access=await payments.GetAccessAsync(userId,ct);
        if(!access.CanViewExistingPlans && !access.CanViewTrialPlans)
        {
            // A new unpaid user with unused trial generations has no history to view yet.
            // Return an empty list so the UI can render the page and offer plan generation.
            if(access.Status=="none" && access.CanGenerate)
            {
                return new PagedResult<MealPlanDto>
                {
                    Items=Array.Empty<MealPlanDto>(),TotalItems=0,TotalCount=0,
                    PageIndex=pageIndex,PageSize=pageSize,TotalPages=0
                };
            }

            throw new MealPlanAccessRequiredException("Your meal-plan viewing period has ended. Purchase or renew a package to continue viewing saved plans.","MEAL_PLAN_VIEW_ACCESS_ENDED");
        }
        var page=await repository.ListAsync(userId,pageIndex,pageSize,!access.CanViewExistingPlans,ct);
        var result=new PagedResult<MealPlanDto>
        {
            Items=page.Items.Select(ToDto).ToList(), TotalItems=page.TotalCount,TotalCount=checked((int)page.TotalCount),
            PageIndex=pageIndex,PageSize=pageSize,TotalPages=Pagination.TotalPages(page.TotalCount,pageSize)
        };
        await routines.EnrichManyAsync(userId,result.Items.ToArray(),ct);
        return result;
    }

    private MealPlanDto ToDto(MealPlan plan)
    {
        var dto=mapper.Map<MealPlanDto>(plan);
        dto.Name=string.IsNullOrWhiteSpace(plan.Name)
            ? plan.StartDate is DateOnly startDate ? MealPlanNaming.Default(startDate) : $"Weekly menu #{plan.Id}"
            : plan.Name;
        dto.Menu=plan.GenerationSnapshot is null ? null : JsonSerializer.Deserialize<WeeklyMenuDto>(plan.GenerationSnapshot,JsonOptions);
        return dto;
    }

    private async Task<MealPlanAccessDto> EnsureCanViewAsync(long userId,CancellationToken ct)
    {
        var access=await payments.GetAccessAsync(userId,ct);
        if(!access.CanViewExistingPlans && !access.CanViewTrialPlans)
            throw new MealPlanAccessRequiredException("Your meal-plan viewing period has ended. Purchase or renew a package to continue viewing saved plans.","MEAL_PLAN_VIEW_ACCESS_ENDED");
        return access;
    }
}
