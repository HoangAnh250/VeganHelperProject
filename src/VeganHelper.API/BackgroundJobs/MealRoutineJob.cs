using Hangfire;
using VeganHelper.BLL.Services.MealPlanning;

namespace VeganHelper.API.BackgroundJobs;

public sealed class MealRoutineJob(MealRoutineReview review)
{
    [Queue("meal-planning")]
    [DisableConcurrentExecution(600)]
    [AutomaticRetry(Attempts=3,DelaysInSeconds=new[]{30,120,600},OnAttemptsExceeded=AttemptsExceededAction.Fail)]
    public Task RunAsync(CancellationToken ct)=>review.RunAsync(ct);
}
