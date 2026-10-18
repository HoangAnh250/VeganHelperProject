using System.Text.Json;
using AutoMapper;
using FluentValidation;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.MealPlanning;
using VeganHelper.BLL.Exceptions;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services.MealPlanning;

public interface IMealPlanningService
{
    Task<MealPlanDto> GenerateAsync(long userId,GenerateMealPlanRequest request,CancellationToken ct);
    Task<MealPlanDto> GetAsync(long userId,long id,CancellationToken ct);
    Task<PagedResult<MealPlanDto>> ListAsync(long userId,int pageIndex,int pageSize,CancellationToken ct);
}

public sealed class MealPlanningService(IMealPlanningRepository repository,IMapper mapper,
    IValidator<GenerateMealPlanRequest> validator,TimeProvider clock,MealRoutineService routines,
    IMealRoutineRepository routineRepository,IMealPlanAccessPolicy access) : IMealPlanningService
{
    private static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web);

    public async Task<MealPlanDto> GenerateAsync(long userId,GenerateMealPlanRequest request,CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request,ct);
        await access.EnsureAsync(userId,ct);
        MealRoutineService.ValidateSettings(request);
        var today=MealRoutineService.LocalToday(clock,request.TimeZoneId);
        if(request.StartDate<today || request.StartDate>today.AddYears(1))
            throw new ArgumentException("Start date must be today or within the next year in your selected timezone.");
        await using var tx=await routineRepository.BeginAsync(ct);
        await routineRepository.LockProfileAsync(userId,ct);
        var input=await repository.LoadAsync(userId,ct);
        GoalSafetyResult? safety=null;
        if(request.Mode=="weight_goal")
        {
            safety=WeightGoalSafety.Evaluate(input.Profile,request.StartDate,request.TargetWeightKg ?? 0,
                request.TargetDate ?? default,request.EligibilityConfirmed);
            if(!safety.Allowed) throw new ArgumentException(string.Join(" ",safety.Reasons));
        }
        var menu=MealPlanGenerator.Generate(input,today,new Random(Random.Shared.Next()),ct,safety?.TargetCalories);
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
            UserId=userId, IsTemplate=false, StartDate=request.StartDate, EndDate=request.StartDate.AddDays(6),
            HeightCm=input.Profile!.HeightCm, WeightKg=input.Profile.WeightKg, BmiValue=menu.Bmi, DietType=menu.DietType,
            AllergiesSnapshot=JsonSerializer.Serialize(input.Allergies,JsonOptions), AvailableIngredientsSnapshot="[]",
            GenerationSource="manual", GenerationSnapshot=JsonSerializer.Serialize(menu,JsonOptions),
            Status="completed", CreatedAt=now, UpdatedAt=now
        };
        var slots=menu.Days.SelectMany(day=>day.Meals.Select(meal=>new MealPlanSchedule
        { DayOfWeek=day.DayOfWeek, MealId=meal.MealId, MealTime=meal.MealTime, PortionMultiplier=meal.PortionMultiplier })).ToList();
        await repository.SaveAsync(plan,slots,ct);
        await routines.SaveRoutineAsync(plan,menu,request,ct);
        await tx.CommitAsync(ct);
        var dto=ToDto(plan); await routines.EnrichAsync(userId,dto,ct); return dto;
    }

    public async Task<MealPlanDto> GetAsync(long userId,long id,CancellationToken ct)
    {
        var dto=ToDto(await repository.GetAsync(userId,id,ct) ?? throw new NotFoundException("Meal plan not found."));
        await routines.EnrichAsync(userId,dto,ct); return dto;
    }

    public async Task<PagedResult<MealPlanDto>> ListAsync(long userId,int pageIndex,int pageSize,CancellationToken ct)
    {
        Pagination.Validate(pageIndex,pageSize);
        var page=await repository.ListAsync(userId,pageIndex,pageSize,ct);
        var result=new PagedResult<MealPlanDto>
        {
            Items=page.Items.Select(ToDto).ToList(), TotalItems=page.TotalCount,TotalCount=checked((int)page.TotalCount),
            PageIndex=pageIndex,PageSize=pageSize,TotalPages=Pagination.TotalPages(page.TotalCount,pageSize)
        };
        foreach(var dto in result.Items) await routines.EnrichAsync(userId,dto,ct);
        return result;
    }

    private MealPlanDto ToDto(MealPlan plan)
    {
        var dto=mapper.Map<MealPlanDto>(plan);
        dto.Menu=plan.GenerationSnapshot is null ? null : JsonSerializer.Deserialize<WeeklyMenuDto>(plan.GenerationSnapshot,JsonOptions);
        return dto;
    }
}
