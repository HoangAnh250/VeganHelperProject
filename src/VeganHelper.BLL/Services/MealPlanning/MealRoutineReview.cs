using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services.MealPlanning;

public sealed class MealRoutineReview(IMealRoutineRepository routines,INotificationRepository notifications,TimeProvider clock)
{
    public static bool NeedsProfileReview(IEnumerable<MealPlanEntry> entries)
    {
        var scheduled=entries.ToList();
        return scheduled.Count>0 && scheduled.Count(e=>e.Status=="followed")/(decimal)scheduled.Count<.7m;
    }
    public async Task RunAsync(CancellationToken ct)
    {
        var now=clock.GetUtcNow().UtcDateTime;
        const int batchSize=100;
        for(var offset=0;;offset+=batchSize)
        {
            var batch=await routines.ActiveAsync(offset,batchSize,ct);
            foreach(var (candidate,plan) in batch)
            {
                ct.ThrowIfCancellationRequested();
                await using var tx=await routines.BeginAsync(ct);
                var routine=await routines.GetAsync(candidate.UserId,plan.Id,true,ct);
                if(routine is null || routine.State is not ("active" or "continued")) continue;
                var entries=await routines.EntriesAsync(plan.Id,ct);
                if(routine.RemindersEnabled)
                foreach(var entry in entries.Where(e=>e.Status=="unlogged" && e.DueAt<=now && e.DueAt>now.AddMinutes(-30)))
                    await Publish(routine.UserId,$"meal:{routine.RootPlanId}:{entry.Date:yyyyMMdd}:{entry.MealTime}",
                        "Your meal is ready on your calendar",$"It is time for {entry.MealTime}. Open your weekly menu and record your meal when you are ready.",now,ct);
                var today=MealRoutineService.LocalToday(clock,routine.TimeZoneId);
                var history=await routines.ReviewHistoryAsync(routine.RootPlanId,ct);
                for(var start=history.Start;start.AddDays(2)<today && start.AddDays(2)<=plan.EndDate;start=start.AddDays(3))
                {
                    var end=start.AddDays(2);
                    var window=history.Entries.Where(e=>e.Date>=start && e.Date<=end).ToList();
                    // A late continuation may leave calendar gaps; review complete scheduled blocks only.
                    if(window.Select(e=>e.Date).Distinct().Count()==3 && NeedsProfileReview(window))
                        await Publish(routine.UserId,$"meal-review:{routine.RootPlanId}:{start:yyyyMMdd}",
                            "Time for a health profile check-in",
                            "Some meals have not been marked as followed over the past three days. Please review and update your health profile so we can check whether your current plan still suits your needs. Any adjustment needs a fresh safety check and your confirmation.",now,ct);
                }
                await tx.CommitAsync(ct);
            }
            if(batch.Count<batchSize) break;
        }
    }
    private Task<long> Publish(long userId,string key,string title,string message,DateTime now,CancellationToken ct)=>
        notifications.PublishAsync(new Notification {UserId=userId,EventKey=key,Type="meal_reminder",Title=title,
            Message=message,TargetUrl="/weekly-menu",CreatedAt=now},ct);
}
