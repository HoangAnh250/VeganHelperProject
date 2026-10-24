using AutoMapper;
using VeganHelper.BLL.Contracts;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VeganHelper.BLL.DTOs.MealPlanning;
using VeganHelper.BLL.Exceptions;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services.MealPlanning;

// Share the payment entitlement check with goal previews and plan revisions.
public interface IMealPlanAccessPolicy { Task EnsureAsync(long userId,CancellationToken ct); }
public sealed class ExistingMealPlanAccessPolicy(IMealPlanPaymentService payments) : IMealPlanAccessPolicy
{
    public async Task EnsureAsync(long userId,CancellationToken ct)
    {
        var access=await payments.GetAccessAsync(userId,ct);
        if(!access.CanGenerate)
            throw new MealPlanAccessRequiredException("Your three free meal-plan trials have been used. Purchase a package to generate more plans.","MEAL_PLAN_TRIAL_LIMIT_REACHED");
    }
}

public sealed class MealRoutineService(IMealRoutineRepository routines,IMealPlanningRepository plans,
    IMealPlanAccessPolicy access,IMapper mapper,TimeProvider clock,IMealPlanPaymentService payments)
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);

    public static string CanonicalZone(string id)=>TimeZoneInfo.TryConvertWindowsIdToIanaId(id,out var iana)?iana:id;

    public static TimeZoneInfo Zone(string id)
    {
        if(string.IsNullOrWhiteSpace(id) || id.Length>100) throw new ArgumentException("Choose a valid timezone.");
        try { return TimeZoneInfo.FindSystemTimeZoneById(CanonicalZone(id)); }
        catch(TimeZoneNotFoundException) { throw new ArgumentException("Choose a valid timezone."); }
        catch(InvalidTimeZoneException) { throw new ArgumentException("Choose a valid timezone."); }
    }
    public static DateOnly LocalToday(TimeProvider clock,string zone)=>DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),Zone(zone)).DateTime);
    public static DateTime Due(DateOnly date,TimeOnly time,string zone)
    {
        var local=date.ToDateTime(time,DateTimeKind.Unspecified); var tz=Zone(zone);
        // A nonexistent DST time moves forward; an ambiguous time uses standard time consistently.
        for(var i=0;tz.IsInvalidTime(local) && i<180;i++) local=local.AddMinutes(1);
        return TimeZoneInfo.ConvertTimeToUtc(local,tz);
    }
    public static string Fingerprint(MealPlanningInput input)=>Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {input.Profile,input.Allergies},Json))));

    public async Task<GoalSafetyResult> PreviewGoalAsync(long userId,GenerateMealPlanRequest request,CancellationToken ct)
    {
        await access.EnsureAsync(userId,ct);
        var input=await plans.LoadAsync(userId,ct);
        var today=LocalToday(clock,request.TimeZoneId);
        return WeightGoalSafety.Evaluate(input.Profile,request.StartDate>today?request.StartDate:today,
            request.TargetWeightKg ?? 0,request.TargetDate ?? default,request.EligibilityConfirmed);
    }

    public static void ValidateSettings(GenerateMealPlanRequest request)
    {
        Zone(request.TimeZoneId);
        if(request.Mode is not ("maintenance" or "weight_goal")) throw new ArgumentException("Unsupported plan mode.");
        if(request.Mode=="maintenance" && (request.TargetWeightKg is not null || request.TargetDate is not null || request.RemindersEnabled))
            throw new ArgumentException("Weight targets and reminders require a weight goal plan.");
        if(!(request.BreakfastTime<request.LunchTime && request.LunchTime<request.DinnerTime))
            throw new ArgumentException("Meal times must be ordered breakfast, lunch, dinner.");
    }

    public async Task SaveRoutineAsync(MealPlan plan,WeeklyMenuDto menu,GenerateMealPlanRequest request,CancellationToken ct,
        long? previous=null,IEnumerable<MealPlanEntry>? oldEntries=null,DateOnly? preserveThrough=null)
    {
        var root=previous is long prior ? (await Required(plan.UserId!.Value,prior,false,ct)).RootPlanId:plan.Id;
        var routine=new MealPlanRoutine {MealPlanId=plan.Id,UserId=plan.UserId!.Value,RootPlanId=root,PreviousPlanId=previous,
            Mode=request.Mode,TargetWeightKg=request.TargetWeightKg,TargetDate=request.TargetDate,
            EligibilityConfirmed=request.EligibilityConfirmed,TimeZoneId=CanonicalZone(request.TimeZoneId),RemindersEnabled=request.RemindersEnabled,
            BreakfastTime=request.BreakfastTime,LunchTime=request.LunchTime,DinnerTime=request.DinnerTime,CreatedAt=clock.GetUtcNow().UtcDateTime};
        var entries=menu.Days.SelectMany(d=>d.Meals.Select(m=>
        {
            var old=oldEntries?.SingleOrDefault(e=>e.Date==d.Date && e.MealTime==m.MealTime);
            var preserve=preserveThrough is DateOnly through && d.Date<=through;
            return new MealPlanEntry {MealPlanId=plan.Id,Date=d.Date,MealTime=m.MealTime,
                DueAt=preserve && old is not null ? old.DueAt : Due(d.Date,m.MealTime switch
                {"breakfast"=>routine.BreakfastTime,"lunch"=>routine.LunchTime,_=>routine.DinnerTime},routine.TimeZoneId),
                Status=preserve ? old?.Status ?? "unlogged":"unlogged",Note=preserve ? old?.Note:null,LoggedAt=preserve ? old?.LoggedAt:null};
        }));
        await routines.AddAsync(routine,entries,ct);
    }

    public async Task EnrichAsync(long userId,MealPlanDto dto,CancellationToken ct)
    {
        if(dto.Menu is not null)
        {
            var meals=dto.Menu.Days.SelectMany(day=>day.Meals).ToList();
            if(meals.Count>0)
            {
                var images=await plans.GetImageUrlsAsync(meals.Select(meal=>meal.MealId).Distinct().ToArray(),ct);
                MealPlanPresentation.Apply(dto.Menu,images);
            }
        }
        var routine=await routines.GetAsync(userId,dto.Id,false,ct);
        if(routine is null) return;
        dto.Routine=mapper.Map<MealRoutineDto>(routine);
        dto.Routine.Entries=mapper.Map<List<MealEntryDto>>(await routines.EntriesAsync(dto.Id,ct));
    }

    public async Task<MealRoutineDto> LogAsync(long userId,long id,LogMealRequest request,CancellationToken ct)
    {
        await using var tx=await routines.BeginAsync(ct);
        var routine=await Required(userId,id,true,ct);
        RequireActive(routine,request.ExpectedVersion,true);
        if(routine.Mode!="weight_goal") throw new ArgumentException("Tracking is available only for weight goal plans.");
        if(request.Status is not ("unlogged" or "followed" or "changed" or "skipped") || request.Note?.Length>500)
            throw new ArgumentException("Choose a supported tracking status and a note of at most 500 characters.");
        var entries=await routines.EntriesAsync(id,ct);
        var entry=entries.SingleOrDefault(e=>e.Date==request.Date && e.MealTime==request.MealTime)
            ?? throw new NotFoundException("Scheduled meal not found.");
        if(entry.DueAt>clock.GetUtcNow().UtcDateTime) throw new ArgumentException("Future meals cannot be recorded yet.");
        entry.Status=request.Status; entry.Note=request.Note?.Trim();
        entry.LoggedAt=request.Status=="unlogged"?null:clock.GetUtcNow().UtcDateTime;
        routine.Version++;
        await routines.SaveAsync(ct); await tx.CommitAsync(ct);
        var result=mapper.Map<MealRoutineDto>(routine); result.Entries=mapper.Map<List<MealEntryDto>>(entries); return result;
    }

    private Task<MealPlanRoutine?> Find(long userId,long id,bool locked,CancellationToken ct)=>routines.GetAsync(userId,id,locked,ct);
    public async Task<MealRoutineDto> UpdateRemindersAsync(long userId,long id,MealReminderSettingsRequest request,CancellationToken ct)
    {
        await using var tx=await routines.BeginAsync(ct);
        var r=await Required(userId,id,true,ct); RequireActive(r,request.ExpectedVersion,true);
        if(r.Mode!="weight_goal") throw new ArgumentException("Reminders require a weight goal plan.");
        ValidateSettings(new GenerateMealPlanRequest {Mode="weight_goal",TimeZoneId=request.TimeZoneId,
            BreakfastTime=request.BreakfastTime,LunchTime=request.LunchTime,DinnerTime=request.DinnerTime});
        var now=clock.GetUtcNow().UtcDateTime;
        var entries=await routines.EntriesAsync(id,ct);
        // Do not move elapsed slots or existing logs when a timezone/time preference changes.
        foreach(var entry in entries.Where(e=>e.DueAt>now && e.Status=="unlogged"))
        {
            var due=Due(entry.Date,entry.MealTime switch {"breakfast"=>request.BreakfastTime,"lunch"=>request.LunchTime,_=>request.DinnerTime},request.TimeZoneId);
            if(due<=now) throw new ArgumentException("The new times would move a future meal into the past. Choose later times.");
            entry.DueAt=due;
        }
        r.TimeZoneId=CanonicalZone(request.TimeZoneId); r.RemindersEnabled=request.Enabled;
        r.BreakfastTime=request.BreakfastTime; r.LunchTime=request.LunchTime; r.DinnerTime=request.DinnerTime; r.Version++;
        await routines.SaveAsync(ct); await tx.CommitAsync(ct);
        var dto=mapper.Map<MealRoutineDto>(r); dto.Entries=mapper.Map<List<MealEntryDto>>(entries); return dto;
    }
    private async Task<MealPlanRoutine> Required(long userId,long id,bool locked,CancellationToken ct)=>
        await Find(userId,id,locked,ct) ?? throw new NotFoundException("Meal plan routine not found.");
    private static void RequireActive(MealPlanRoutine r,int version,bool allowContinued=false)
    {
        if((r.State!="active" && !(allowContinued && r.State=="continued")) || r.Version!=version) throw new ConflictException("This plan changed. Reload it before continuing.");
    }

    public async Task<RevisionPreview> PreviewRevisionAsync(long userId,long id,bool nextWeek,CancellationToken ct,
        decimal? targetWeight=null,DateOnly? targetDate=null,bool? confirmed=null)
    {
        await access.EnsureAsync(userId,ct);
        var routine=await Required(userId,id,false,ct);
        var plan=await plans.GetAsync(userId,id,false,ct) ?? throw new NotFoundException("Meal plan not found.");
        var input=await plans.LoadAsync(userId,ct);
        return BuildPreview(routine,plan,input,nextWeek,targetWeight,targetDate,confirmed);
    }

    private RevisionPreview BuildPreview(MealPlanRoutine r,MealPlan plan,MealPlanningInput input,bool nextWeek,
        decimal? targetWeight,DateOnly? targetDate,bool? confirmed)
    {
        if(r.Mode!="weight_goal" || r.State!="active") throw new ConflictException("Choose an active weight goal plan.");
        var today=LocalToday(clock,r.TimeZoneId);
        var effective=nextWeek ? plan.EndDate!.Value.AddDays(1) : (today>=plan.StartDate ? today.AddDays(1):plan.StartDate!.Value);
        var asOf=effective>today?effective:today;
        var safety=WeightGoalSafety.Evaluate(input.Profile,asOf,targetWeight ?? r.TargetWeightKg!.Value,
            targetDate ?? r.TargetDate!.Value,confirmed ?? r.EligibilityConfirmed);
        var reasons=new List<string>(safety.Reasons);
        if(!nextWeek && effective>plan.EndDate) reasons.Add("This week has finished. Continue with a new week instead.");
        if(nextWeek && effective<today) reasons.Add("This next week is already in the past. Create a new plan from today.");
        if((targetDate ?? r.TargetDate)<effective.AddDays(6) && nextWeek) reasons.Add("Less than a full week remains before the goal date. Choose a new safe timeline.");
        var old=JsonSerializer.Deserialize<WeeklyMenuDto>(plan.GenerationSnapshot!,Json)!;
        return new(reasons.Count==0,reasons,r.Version,Fingerprint(input),old.TargetDailyCalories,safety.TargetCalories,
            safety.Tdee,safety.DailyAdjustment,effective,Math.Abs(old.TargetDailyCalories-safety.TargetCalories)>=100m,
            nextWeek,safety.PolicyVersion);
    }

    public async Task<MealPlanDto> ConfirmRevisionAsync(long userId,long id,RevisionRequest request,CancellationToken ct)
    {
        await access.EnsureAsync(userId,ct);
        await using var tx=await routines.BeginAsync(ct);
        var r=await Required(userId,id,true,ct); RequireActive(r,request.ExpectedVersion);
        await routines.LockProfileAsync(userId,ct);
        var original=await plans.GetAsync(userId,id,false,ct) ?? throw new NotFoundException("Meal plan not found.");
        var input=await plans.LoadAsync(userId,ct);
        if(Fingerprint(input)!=request.ProfileFingerprint) throw new ConflictException("Your health profile changed. Review a new proposal before confirming.");
        var preview=BuildPreview(r,original,input,request.NextWeek,request.TargetWeightKg,request.TargetDate,request.EligibilityConfirmed);
        if(!preview.Allowed) throw new ArgumentException(string.Join(" ",preview.Reasons));
        var today=LocalToday(clock,r.TimeZoneId);
        var safety=WeightGoalSafety.Evaluate(input.Profile,preview.EffectiveFrom>today?preview.EffectiveFrom:today,
            request.TargetWeightKg ?? r.TargetWeightKg!.Value,request.TargetDate ?? r.TargetDate!.Value,request.EligibilityConfirmed);
        var fresh=MealPlanGenerator.Generate(input,today,new Random(Random.Shared.Next()),ct,safety.TargetCalories);
        fresh.Goal="weight_goal"; fresh.Notes=GoalNotes();
        var old=JsonSerializer.Deserialize<WeeklyMenuDto>(original.GenerationSnapshot!,Json)!;
        var start=request.NextWeek?preview.EffectiveFrom:original.StartDate!.Value;
        foreach(var day in fresh.Days) { day.Date=start.AddDays(day.DayIndex-1); day.DayOfWeek=(byte)(((int)day.Date.DayOfWeek+6)%7+1); }
        WeightGoalSafety.ValidateGeneratedDays(fresh.Days.Where(d=>d.Date>=preview.EffectiveFrom),safety,input.Profile!.BiologicalSex!);
        if(!request.NextWeek) fresh.Days=fresh.Days.Select(d=>d.Date<preview.EffectiveFrom?old.Days.Single(o=>o.Date==d.Date):d).ToList();
        var now=clock.GetUtcNow().UtcDateTime;
        var result=new MealPlan {UserId=userId,StartDate=start,EndDate=start.AddDays(6),DietType=fresh.DietType,
            HeightCm=input.Profile!.HeightCm,WeightKg=input.Profile.WeightKg,BmiValue=fresh.Bmi,
            AllergiesSnapshot=JsonSerializer.Serialize(input.Allergies,Json),AvailableIngredientsSnapshot="[]",
            GenerationSource="manual",GenerationSnapshot=JsonSerializer.Serialize(fresh,Json),Status="completed",CreatedAt=now,UpdatedAt=now};
        var entitlement=await payments.GetAccessAsync(userId,ct);
        if(entitlement.Status=="active") await plans.SaveAsync(result,Slots(fresh),ct);
        else if(!await plans.TrySaveTrialAsync(result,Slots(fresh),VeganHelper.BLL.Services.MealPlanAccessPolicy.TrialGenerationLimit,ct))
            throw new MealPlanAccessRequiredException("Your three free meal-plan trials have been used. Purchase a package to generate more plans.","MEAL_PLAN_TRIAL_LIMIT_REACHED");
        await SaveRoutineAsync(result,fresh,new GenerateMealPlanRequest {Mode="weight_goal",TimeZoneId=r.TimeZoneId,
            TargetWeightKg=request.TargetWeightKg ?? r.TargetWeightKg,TargetDate=request.TargetDate ?? r.TargetDate,
            EligibilityConfirmed=request.EligibilityConfirmed,RemindersEnabled=r.RemindersEnabled,
            BreakfastTime=r.BreakfastTime,LunchTime=r.LunchTime,DinnerTime=r.DinnerTime},ct,id,
            await routines.EntriesAsync(id,ct),request.NextWeek?null:today);
        r.State=request.NextWeek?"continued":"superseded"; r.Version++;
        await routines.SaveAsync(ct); await tx.CommitAsync(ct);
        var dto=mapper.Map<MealPlanDto>(result); dto.Menu=fresh; await EnrichAsync(userId,dto,ct); return dto;
    }
    public static List<string> GoalNotes()=>["Calorie targets are estimates, not a guarantee of weight change or nutritional adequacy.",
        "Weight goal screening uses conservative application rules and cannot replace individual professional advice.",
        "Recheck your health profile regularly. Any future menu adjustment requires a fresh safety check and your confirmation."];
    public static List<MealPlanSchedule> Slots(WeeklyMenuDto menu)=>menu.Days.SelectMany(d=>d.Meals.Select(m=>new MealPlanSchedule
        {DayOfWeek=d.DayOfWeek,MealId=m.MealId,MealTime=m.MealTime,PortionMultiplier=m.PortionMultiplier})).ToList();
}
