using Hangfire;
using VeganHelper.BLL.Contracts;

namespace VeganHelper.API.BackgroundJobs;

public sealed class PostSummaryJob(IPostSummaryService service)
{
    [AutomaticRetry(Attempts = 0)]
    public Task RunAsync(long postId) => service.ProcessAsync(postId, CancellationToken.None);
}

public sealed class HangfirePostSummaryQueue(IBackgroundJobClient jobs) : IPostSummaryQueue
{
    public void Enqueue(long postId) => jobs.Enqueue<PostSummaryJob>(job => job.RunAsync(postId));
}

public sealed class DisabledPostSummaryQueue : IPostSummaryQueue
{
    public void Enqueue(long postId) => throw new InvalidOperationException(
        "Video summary is disabled. Configure GroqSummary on the Backend.");
}
