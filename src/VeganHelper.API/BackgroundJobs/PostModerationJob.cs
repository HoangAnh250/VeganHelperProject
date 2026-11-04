using Hangfire;
using VeganHelper.BLL.Services;

namespace VeganHelper.API.BackgroundJobs;

public sealed class PostModerationJob(PostModerationProcessor processor)
{
    [DisableConcurrentExecution(360), AutomaticRetry(Attempts = 0)]
    public Task RunAsync(CancellationToken cancellationToken) => processor.RunAsync(cancellationToken);
}
