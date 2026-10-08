using System.Collections.Concurrent;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VeganHelper.BLL.Mapping;
using VeganHelper.BLL.Services;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Integrations.Moderation;
using VeganHelper.DAL.Persistence;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class PostModerationProcessorTests(Member2ApiFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2030, 1, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FixedClock();
    private readonly List<long> ownedScans = [];

    public async Task InitializeAsync()
    {
        await using var db = fixture.Context();
        // The shared collection database also contains posts created by API tests. Make their
        // unfinished jobs ineligible before exercising this processor's global claim queue.
        await db.Set<PostModerationScan>().Where(s => s.State == "queued" || s.State == "processing")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "obsolete")
                .SetProperty(x => x.LeaseToken, (Guid?)null).SetProperty(x => x.LeaseUntil, (DateTime?)null));
        await SetAutoPublishAsync(false);
    }

    public async Task DisposeAsync()
    {
        await using var db = fixture.Context();
        await db.Set<PostModerationScan>().Where(s => ownedScans.Contains(s.Id) && (s.State == "queued" || s.State == "processing"))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "obsolete")
                .SetProperty(x => x.LeaseToken, (Guid?)null).SetProperty(x => x.LeaseUntil, (DateTime?)null));
        var postIds = await db.Set<PostModerationScan>().Where(s => ownedScans.Contains(s.Id)).Select(s => s.PostId).ToListAsync();
        await db.Posts.Where(p => postIds.Contains(p.Id)).ExecuteUpdateAsync(p =>
            p.SetProperty(x => x.IsDeleted, true).SetProperty(x => x.DeletedAt, Now.UtcDateTime));
        await SetAutoPublishAsync(false);
    }

    [Fact]
    public async Task DisabledProvider_LeavesQueuedScanUnclaimed()
    {
        var job = await SeedAsync();
        var client = new FakeClient((_, _) => throw new Xunit.Sdk.XunitException("Disabled moderation called AI"));

        await RunAsync(client, enabled: false);

        await using var db = fixture.Context();
        var scan = await db.Set<PostModerationScan>().SingleAsync(s => s.Id == job.ScanId);
        Assert.Equal("queued", scan.State);
        Assert.Equal(0, scan.Attempts);
        Assert.Null(scan.LeaseToken);
        Assert.Null(scan.LeaseUntil);
        await AssertNoDecisionOrNotificationAsync(db, job);
    }

    [Fact]
    public async Task SafeResult_WithAutoPublishOff_RecordsCompleteAssessmentAndKeepsPending()
    {
        var job = await SeedAsync();

        await RunAsync(new FakeClient((_, _) => Task.FromResult(Result())));

        await using var db = fixture.Context();
        var scan = await db.Set<PostModerationScan>().SingleAsync(s => s.Id == job.ScanId);
        Assert.Equal("safe", scan.State);
        Assert.Equal(1, scan.Attempts);
        Assert.Equal(.99, scan.Confidence);
        Assert.Equal("gemini-3.8-flash", scan.Model);
        Assert.Equal("post-moderation.v1", scan.PromptVersion);
        Assert.Equal("Plant based content.", scan.Summary);
        Assert.Equal(100, scan.InputTokens);
        Assert.Equal(35, scan.OutputTokens);
        Assert.Equal(42, scan.LatencyMs);
        Assert.NotNull(scan.CompletedAt);
        Assert.Null(scan.LeaseToken);
        Assert.Null(scan.LeaseUntil);
        Assert.Equal("pending_review", (await db.Posts.SingleAsync(p => p.Id == job.PostId)).Status);
        Assert.False(await db.Flags.AnyAsync(f => f.PostId == job.PostId));
        await AssertNoDecisionOrNotificationAsync(db, job);
        var usage = await db.Set<AiUsage>().SingleAsync(u => u.UserId == job.AuthorId);
        Assert.Equal("gemini", usage.Provider);
        Assert.Equal("gemini-3.8-flash", usage.ModelName);
        Assert.Equal(100, usage.InputTokens);
        Assert.Equal(35, usage.OutputTokens);
    }

    [Fact]
    public async Task SafeResult_WithAutoPublishOn_PublishesOnceWithDecisionNotificationAndPushOutbox()
    {
        var job = await SeedAsync();
        await SetAutoPublishAsync(true);
        var subscriptionId = await SubscribeAsync(job.AuthorId);
        var client = new FakeClient((_, _) => Task.FromResult(Result()));

        await RunAsync(client);
        await RunAsync(client);

        await using var db = fixture.Context();
        var post = await db.Posts.SingleAsync(p => p.Id == job.PostId);
        Assert.Equal("published", post.Status);
        Assert.Equal(1, post.ContentRevision);
        Assert.Equal("safe", (await db.Set<PostModerationScan>().SingleAsync(s => s.Id == job.ScanId)).State);
        var decision = await db.Set<PostModerationDecision>().SingleAsync(d => d.PostId == job.PostId);
        Assert.Equal("approve", decision.Action);
        Assert.Equal("ai", decision.ActorType);
        Assert.Equal(1, decision.Revision);
        Assert.Equal(job.ScanId, decision.ScanId);
        Assert.Null(decision.AdminId);
        var notification = await db.Set<Notification>().SingleAsync(n => n.UserId == job.AuthorId);
        Assert.Equal($"moderation:{decision.Id}", notification.EventKey);
        Assert.Equal("post_review", notification.Type);
        Assert.Equal($"/posts/{job.PostId}", notification.TargetUrl);
        Assert.Equal(decision.Reason, notification.Message);
        var delivery = await db.Set<NotificationPushDelivery>().SingleAsync(d => d.NotificationId == notification.Id);
        Assert.Equal(subscriptionId, delivery.SubscriptionId);
        Assert.Equal("pending", delivery.State);
        Assert.Single(client.Inputs);
    }

    [Theory]
    [InlineData(.89)]
    [InlineData(.0)]
    public async Task SafeResult_BelowConfidenceThreshold_RequiresManualReviewWithoutPublishing(double confidence)
    {
        var job = await SeedAsync();
        await SetAutoPublishAsync(true);

        await RunAsync(new FakeClient((_, _) => Task.FromResult(Result(confidence: confidence))));

        await using var db = fixture.Context();
        var scan = await db.Set<PostModerationScan>().SingleAsync(s => s.Id == job.ScanId);
        Assert.Equal("manual_required", scan.State);
        Assert.Equal(confidence, scan.Confidence);
        Assert.Equal("pending_review", (await db.Posts.SingleAsync(p => p.Id == job.PostId)).Status);
        await AssertNoDecisionOrNotificationAsync(db, job);
    }

    [Theory]
    [InlineData("flagged", "flagged")]
    [InlineData("uncertain", "flagged")]
    public async Task UnsafeOrUncertainResult_CreatesCurrentRevisionAiFlagWithEvidence(string verdict, string state)
    {
        var job = await SeedAsync();
        await SetAutoPublishAsync(true);

        await RunAsync(new FakeClient((_, _) => Task.FromResult(Result(verdict))));

        await using var db = fixture.Context();
        var scan = await db.Set<PostModerationScan>().SingleAsync(s => s.Id == job.ScanId);
        Assert.Equal(state, scan.State);
        using var evidence = JsonDocument.Parse(scan.FindingsJson);
        var finding = Assert.Single(evidence.RootElement.EnumerateArray());
        Assert.Contains("Animal ingredient", finding.ToString());
        var flag = await db.Flags.SingleAsync(f => f.PostId == job.PostId);
        Assert.Equal("ai", flag.SourceType);
        Assert.Equal("pending", flag.Status);
        Assert.Equal(1, flag.PostRevision);
        Assert.Equal(job.ScanId, flag.ModerationScanId);
        Assert.Equal("gemini-3.8-flash", flag.AiModelName);
        Assert.False(string.IsNullOrWhiteSpace(flag.Reason));
        Assert.Null(flag.ReporterId);
        Assert.Equal("pending_review", (await db.Posts.SingleAsync(p => p.Id == job.PostId)).Status);
        await AssertNoDecisionOrNotificationAsync(db, job);
    }

    [Fact]
    public async Task SafeResult_AtConfidenceThreshold_CanPublish()
    {
        var job = await SeedAsync();
        await SetAutoPublishAsync(true);

        await RunAsync(new FakeClient((_, _) => Task.FromResult(Result(confidence: .9))));

        await using var db = fixture.Context();
        Assert.Equal("published", (await db.Posts.SingleAsync(p => p.Id == job.PostId)).Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    public async Task SafeResult_WhenCurrentAiFlagUnresolved_RequiresHumanReview(int? revision)
    {
        var job = await SeedAsync(); await SetAutoPublishAsync(true);
        await using (var db = fixture.Context())
        {
            db.Flags.Add(new Flag { PostId = job.PostId, PostRevision = revision, SourceType = "ai", AiModelName = "legacy-model",
                Reason = "Existing evidence needs a human decision", Status = "pending", CreatedAt = Now.UtcDateTime });
            await db.SaveChangesAsync();
        }
        await RunAsync(new FakeClient((_, _) => Task.FromResult(Result())));
        await using var verify = fixture.Context();
        Assert.Equal("pending_review", (await verify.Posts.SingleAsync(p => p.Id == job.PostId)).Status);
        var scan = await verify.Set<PostModerationScan>().SingleAsync(s => s.Id == job.ScanId);
        Assert.Equal("manual_required", scan.State); Assert.Equal("pending_ai_flag", scan.ErrorCode);
        Assert.Equal("pending", (await verify.Flags.SingleAsync(f => f.PostId == job.PostId)).Status);
        await AssertNoDecisionOrNotificationAsync(verify, job);
    }
    [Fact]
    public async Task SafeResult_WhenFlagBelongsToPreviousRevision_DoesNotBlockCurrentScan()
    {
        var job = await SeedAsync(); await SetAutoPublishAsync(true);
        await using (var db = fixture.Context())
        {
            await db.Posts.Where(p => p.Id == job.PostId).ExecuteUpdateAsync(s => s.SetProperty(p => p.ContentRevision, 2));
            await db.Set<PostModerationScan>().Where(s => s.Id == job.ScanId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Revision, 2));
            db.Flags.Add(new Flag { PostId = job.PostId, PostRevision = 1, SourceType = "ai", AiModelName = "legacy-model",
                Reason = "Old revision evidence", Status = "pending", CreatedAt = Now.UtcDateTime });
            await db.SaveChangesAsync();
        }
        await RunAsync(new FakeClient((_, _) => Task.FromResult(Result())));
        await using var verify = fixture.Context();
        Assert.Equal("published", (await verify.Posts.SingleAsync(p => p.Id == job.PostId)).Status);
        Assert.Empty(await new ModerationRepository(verify).CurrentFlagsAsync(job.PostId, default));
    }
    [Theory]
    [InlineData(.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task AutoPublish_WhenThresholdConfigurationUnsafe_NeverPublishesLowConfidence(double threshold)
    {
        var job = await SeedAsync(); await SetAutoPublishAsync(true);
        await using var db = fixture.Context();
        var mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var processor = new PostModerationProcessor(new ModerationRepository(db), new FakeClient((_, _) => Task.FromResult(Result(confidence: .8))),
            new NotificationService(new NotificationRepository(db), mapper, Clock),
            Options.Create(new ModerationAiOptions { Enabled = true, MinAutoPublishConfidence = threshold }), Clock);
        await processor.RunAsync(default);
        db.ChangeTracker.Clear();
        Assert.Equal("pending_review", (await db.Posts.SingleAsync(p => p.Id == job.PostId)).Status);
        Assert.Equal("manual_required", (await db.Set<PostModerationScan>().SingleAsync(s => s.Id == job.ScanId)).State);
        await AssertNoDecisionOrNotificationAsync(db, job);
    }

    [Theory]
    [InlineData("provider_timeout")]
    [InlineData("provider_error")]
    public async Task TransientProviderFailure_RequeuesWithBackoffWithoutPublishing(string code)
    {
        var job = await SeedAsync();
        var client = new FakeClient((_, _) => throw new ModerationAiException(code, true));

        await RunAsync(client);

        await using var db = fixture.Context();
        var scan = await db.Set<PostModerationScan>().SingleAsync(s => s.Id == job.ScanId);
        Assert.Equal("queued", scan.State);
        Assert.Equal(1, scan.Attempts);
        Assert.Equal(code, scan.ErrorCode);
        Assert.True(scan.NextAttemptAt > Now.UtcDateTime);
        Assert.Null(scan.LeaseToken);
        Assert.Null(scan.LeaseUntil);
        Assert.Null(scan.CompletedAt);
        Assert.Single(client.Inputs);
        await AssertNoDecisionOrNotificationAsync(db, job);
    }

    [Fact]
    public async Task FifthTransientFailure_StopsRetryingAndRequiresHumanAttention()
    {
        var job = await SeedAsync(attempts: 4);
        var client = new FakeClient((_, _) => throw new ModerationAiException("provider_timeout", true));

        await RunAsync(client);
        await RunAsync(client);

        await using var db = fixture.Context();
        var scan = await db.Set<PostModerationScan>().SingleAsync(s => s.Id == job.ScanId);
        Assert.Equal("failed", scan.State);
        Assert.Equal(5, scan.Attempts);
        Assert.Equal("provider_timeout", scan.ErrorCode);
        Assert.NotNull(scan.CompletedAt);
        Assert.Null(scan.LeaseToken);
        Assert.Null(scan.LeaseUntil);
        Assert.Single(client.Inputs);
        await AssertNoDecisionOrNotificationAsync(db, job);
    }

    [Theory]
    [InlineData("unsupported_media")]
    [InlineData("invalid_response")]
    [InlineData("unsafe_media_url")]
    public async Task NonRetryableBoundaryFailure_RequiresManualReview(string code)
    {
        var job = await SeedAsync();
        var client = new FakeClient((_, _) => throw new ModerationAiException(code, false));

        await RunAsync(client);

        await using var db = fixture.Context();
        var scan = await db.Set<PostModerationScan>().SingleAsync(s => s.Id == job.ScanId);
        Assert.Equal("manual_required", scan.State);
        Assert.Equal(code, scan.ErrorCode);
        Assert.Equal(1, scan.Attempts);
        Assert.NotNull(scan.CompletedAt);
        Assert.Null(scan.LeaseToken);
        Assert.Null(scan.LeaseUntil);
        await AssertNoDecisionOrNotificationAsync(db, job);
    }

    [Fact]
    public async Task ConcurrentWorkers_OnlyOneCallsAiForTheSameLiveLease()
    {
        var job = await SeedAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var client = new FakeClient(async (_, ct) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
            return Result();
        });

        var first = RunAsync(client, ct: deadline.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), deadline.Token);
            await RunAsync(client, ct: deadline.Token);
            Assert.Single(client.Inputs);
        }
        finally
        {
            release.TrySetResult();
            await first;
        }

        await using var db = fixture.Context();
        var scan = await db.Set<PostModerationScan>().SingleAsync(s => s.Id == job.ScanId);
        Assert.Equal(1, scan.Attempts);
        Assert.Equal("safe", scan.State);
    }

    [Fact]
    public async Task ExpiredProcessingLease_IsReclaimedAndAssessed()
    {
        var job = await SeedAsync(state: "processing", attempts: 1, leaseUntil: Now.UtcDateTime.AddSeconds(-1));

        await RunAsync(new FakeClient((_, _) => Task.FromResult(Result())));

        await using var db = fixture.Context();
        var scan = await db.Set<PostModerationScan>().SingleAsync(s => s.Id == job.ScanId);
        Assert.Equal("safe", scan.State);
        Assert.Equal(2, scan.Attempts);
        Assert.Null(scan.LeaseToken);
        Assert.Null(scan.LeaseUntil);
    }

    [Fact]
    public async Task SupersededLeaseOwner_CannotOverwriteTheNewOwnersFlaggedResult()
    {
        var job = await SeedAsync();
        await SetAutoPublishAsync(true);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var firstClient = new FakeClient(async (_, ct) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
            return Result();
        });
        var first = RunAsync(firstClient, ct: deadline.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), deadline.Token);
            await using (var concurrent = fixture.Context())
                await concurrent.Set<PostModerationScan>().Where(s => s.Id == job.ScanId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseUntil, Now.UtcDateTime.AddSeconds(-1)), deadline.Token);
            await RunAsync(new FakeClient((_, _) => Task.FromResult(Result("flagged"))), ct: deadline.Token);
        }
        finally
        {
            release.TrySetResult();
            await first;
        }

        await using var db = fixture.Context();
        var scan = await db.Set<PostModerationScan>().SingleAsync(s => s.Id == job.ScanId);
        Assert.Equal("flagged", scan.State);
        Assert.Equal(2, scan.Attempts);
        Assert.Single(await db.Flags.Where(f => f.PostId == job.PostId).ToListAsync());
        Assert.Equal("pending_review", (await db.Posts.SingleAsync(p => p.Id == job.PostId)).Status);
        await AssertNoDecisionOrNotificationAsync(db, job);
    }

    [Fact]
    public async Task ExpiredFinalAttemptLease_IsFailedWithoutAnotherAiCall()
    {
        var job = await SeedAsync(state: "processing", attempts: 5, leaseUntil: Now.UtcDateTime.AddSeconds(-1));
        var client = new FakeClient((_, _) => throw new Xunit.Sdk.XunitException("Exhausted lease called AI"));

        await RunAsync(client);

        await using var db = fixture.Context();
        var scan = await db.Set<PostModerationScan>().SingleAsync(s => s.Id == job.ScanId);
        Assert.Equal("failed", scan.State);
        Assert.Equal(5, scan.Attempts);
        Assert.Equal("lease_exhausted", scan.ErrorCode);
        Assert.NotNull(scan.CompletedAt);
        Assert.Null(scan.LeaseToken);
        Assert.Null(scan.LeaseUntil);
        await AssertNoDecisionOrNotificationAsync(db, job);
    }

    [Fact]
    public async Task CancellationDuringAi_PropagatesAndLeavesRecoverableLease()
    {
        var job = await SeedAsync();
        using var cancellation = new CancellationTokenSource();
        var client = new FakeClient(async (_, ct) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.Infinite, ct);
            return Result();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(client, ct: cancellation.Token));

        await using var db = fixture.Context();
        var scan = await db.Set<PostModerationScan>().SingleAsync(s => s.Id == job.ScanId);
        Assert.Equal("processing", scan.State);
        Assert.Equal(1, scan.Attempts);
        Assert.NotNull(scan.LeaseToken);
        Assert.True(scan.LeaseUntil > Now.UtcDateTime);
        Assert.Null(scan.CompletedAt);
        await AssertNoDecisionOrNotificationAsync(db, job);
    }

    [Theory]
    [InlineData("revision", "safe")]
    [InlineData("revision", "flagged")]
    [InlineData("deleted", "safe")]
    [InlineData("deleted", "flagged")]
    [InlineData("manual", "safe")]
    [InlineData("manual", "flagged")]
    public async Task ContentOrManualDecisionChangesDuringAi_ObsoleteTheResultWithoutSideEffects(string change, string verdict)
    {
        var job = await SeedAsync();
        await SetAutoPublishAsync(true);
        long? manualDecisionId = null;
        var client = new FakeClient(async (_, _) =>
        {
            await using var concurrent = fixture.Context();
            if (change == "revision")
                await concurrent.Posts.Where(p => p.Id == job.PostId).ExecuteUpdateAsync(p =>
                    p.SetProperty(x => x.ContentRevision, 2).SetProperty(x => x.Title, "New revision"));
            else if (change == "deleted")
                await concurrent.Posts.Where(p => p.Id == job.PostId).ExecuteUpdateAsync(p =>
                    p.SetProperty(x => x.IsDeleted, true).SetProperty(x => x.DeletedAt, Now.UtcDateTime));
            else
            {
                await concurrent.Posts.Where(p => p.Id == job.PostId).ExecuteUpdateAsync(p =>
                    p.SetProperty(x => x.Status, "rejected").SetProperty(x => x.UpdatedAt, Now.UtcDateTime));
                var decision = new PostModerationDecision
                {
                    PostId = job.PostId, Revision = 1, Action = "reject", ActorType = "admin",
                    AdminId = await UserAsync(2), Reason = "Human decision wins.", CreatedAt = Now.UtcDateTime
                };
                concurrent.Add(decision);
                await concurrent.SaveChangesAsync();
                manualDecisionId = decision.Id;
            }
            return Result(verdict);
        });

        await RunAsync(client);

        await using var db = fixture.Context();
        var scan = await db.Set<PostModerationScan>().SingleAsync(s => s.Id == job.ScanId);
        Assert.Equal("obsolete", scan.State);
        Assert.Null(scan.LeaseToken);
        Assert.Null(scan.LeaseUntil);
        Assert.NotNull(scan.CompletedAt);
        var post = await db.Posts.SingleAsync(p => p.Id == job.PostId);
        Assert.Equal(change == "manual" ? "rejected" : "pending_review", post.Status);
        Assert.Equal(change == "revision" ? 2 : 1, post.ContentRevision);
        Assert.Equal(change == "deleted", post.IsDeleted);
        Assert.False(await db.Flags.AnyAsync(f => f.PostId == job.PostId));
        var decisions = await db.Set<PostModerationDecision>().Where(d => d.PostId == job.PostId).ToListAsync();
        if (manualDecisionId.HasValue) Assert.Equal(manualDecisionId, Assert.Single(decisions).Id);
        else Assert.Empty(decisions);
        Assert.False(await db.Set<Notification>().AnyAsync(n => n.UserId == job.AuthorId));
    }

    [Fact]
    public async Task AutoPublishDisabledWhileAiRuns_IsRespectedAtFinalization()
    {
        var job = await SeedAsync();
        await SetAutoPublishAsync(true);

        await RunAsync(new FakeClient(async (_, _) => { await SetAutoPublishAsync(false); return Result(); }));

        await using var db = fixture.Context();
        Assert.Equal("safe", (await db.Set<PostModerationScan>().SingleAsync(s => s.Id == job.ScanId)).State);
        Assert.Equal("pending_review", (await db.Posts.SingleAsync(p => p.Id == job.PostId)).Status);
        await AssertNoDecisionOrNotificationAsync(db, job);
    }

    [Fact]
    public async Task Run_ClaimsAtMostTwoJobsPerBatch()
    {
        var jobs = new[] { await SeedAsync(), await SeedAsync(), await SeedAsync() };
        var ids = jobs.Select(j => j.ScanId).ToArray();
        var client = new FakeClient((_, _) => Task.FromResult(Result()));

        await RunAsync(client);

        await using var db = fixture.Context();
        var scans = await db.Set<PostModerationScan>().Where(s => ids.Contains(s.Id)).ToListAsync();
        Assert.Equal(2, scans.Count(s => s.State == "safe" && s.Attempts == 1));
        Assert.Single(scans, s => s.State == "queued" && s.Attempts == 0);
        Assert.Equal(2, client.Inputs.Count);
    }

    [Fact]
    public async Task NotificationInsertFailure_RollsBackPublicationDecisionAssessmentAndPushOutbox()
    {
        var job = await SeedAsync();
        await SetAutoPublishAsync(true);
        var subscriptionId = await SubscribeAsync(job.AuthorId);
        var failure = new FailNotificationInsert();

        // Either propagation or worker recovery is allowed; durable state must stay atomic.
        await Record.ExceptionAsync(() => RunAsync(new FakeClient((_, _) => Task.FromResult(Result())), interceptor: failure));

        Assert.True(failure.Triggered, "The test must reach the real notification INSERT before injecting its failure.");
        await using var db = fixture.Context();
        Assert.Equal("pending_review", (await db.Posts.SingleAsync(p => p.Id == job.PostId)).Status);
        var scan = await db.Set<PostModerationScan>().SingleAsync(s => s.Id == job.ScanId);
        Assert.NotEqual("safe", scan.State);
        Assert.Null(scan.CompletedAt);
        Assert.False(await db.Set<NotificationPushDelivery>().AnyAsync(d => d.SubscriptionId == subscriptionId));
        Assert.False(await db.Flags.AnyAsync(f => f.PostId == job.PostId));
        await AssertNoDecisionOrNotificationAsync(db, job);
    }

    private async Task RunAsync(FakeClient client, bool enabled = true, DbCommandInterceptor? interceptor = null, CancellationToken ct = default)
    {
        await using var db = interceptor is null ? fixture.Context() : new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.Connection).AddInterceptors(interceptor).Options);
        var mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var notifications = new NotificationService(new NotificationRepository(db), mapper, Clock);
        var processor = new PostModerationProcessor(new ModerationRepository(db), client, notifications,
            Options.Create(new ModerationAiOptions { Enabled = enabled, MinAutoPublishConfidence = .9 }), Clock);
        await processor.RunAsync(ct);
    }

    private async Task<Job> SeedAsync(string state = "queued", int attempts = 0, DateTime? leaseUntil = null)
    {
        var authorId = await UserAsync();
        await using var db = fixture.Context();
        var post = new Post
        {
            AuthorId = authorId, PostType = "community", Title = "Vegan community post", Content = "Full vegan content",
            Status = "pending_review", ContentRevision = 1, CreatedAt = Now.UtcDateTime
        };
        var scan = new PostModerationScan
        {
            Post = post, Revision = 1, State = state, Attempts = attempts, CreatedAt = Now.UtcDateTime,
            NextAttemptAt = Now.UtcDateTime.AddMinutes(-1), LeaseUntil = leaseUntil,
            LeaseToken = state == "processing" ? Guid.NewGuid() : null
        };
        db.Add(scan);
        await db.SaveChangesAsync();
        ownedScans.Add(scan.Id);
        return new(post.Id, scan.Id, authorId);
    }

    private async Task<long> UserAsync(int role = 1)
    {
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User
        {
            Username = "processor_" + suffix, Email = suffix + "@example.invalid", RoleId = role,
            IsActive = true, CreatedAt = Now.UtcDateTime
        };
        db.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task SetAutoPublishAsync(bool enabled)
    {
        await using var db = fixture.Context();
        await db.Set<PostModerationSettings>().Where(s => s.Id == 1)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AutoPublishEnabled, enabled));
    }

    private async Task<long> SubscribeAsync(long authorId)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var point = key.ExportParameters(false).Q;
        var raw = new byte[65]; raw[0] = 4; point.X!.CopyTo(raw, 1); point.Y!.CopyTo(raw, 33);
        static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var endpoint = "https://fcm.googleapis.com/fcm/send/" + Guid.NewGuid().ToString("N");
        await using var db = fixture.Context();
        return await new NotificationRepository(db).SubscribeAsync(new BrowserPushSubscription
        {
            UserId = authorId, Endpoint = endpoint, EndpointHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint))),
            P256dh = Encode(raw), Auth = Encode(RandomNumberGenerator.GetBytes(16)), IsActive = true, UpdatedAt = Now.UtcDateTime
        }, default);
    }

    private static async Task AssertNoDecisionOrNotificationAsync(AppDbContext db, Job job)
    {
        Assert.False(await db.Set<PostModerationDecision>().AnyAsync(d => d.PostId == job.PostId));
        Assert.False(await db.Set<Notification>().AnyAsync(n => n.UserId == job.AuthorId));
    }

    private static ModerationAiResult Result(string verdict = "safe", double confidence = .99) => new(
        verdict, confidence, verdict == "safe" ? "Plant based content." : "Animal ingredient needs review.",
        verdict == "safe" ? [] : [new ModerationAiFinding("animal_ingredient_recipe", "content", "Animal ingredient", null, "Animal ingredient in content.")],
        "gemini-3.8-flash", "post-moderation.v1", 100, 35, 42);

    private sealed record Job(long PostId, long ScanId, long AuthorId);
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }

    private sealed class FakeClient(Func<ModerationAiInput, CancellationToken, Task<ModerationAiResult>> review) : IPostModerationAiClient
    {
        public ConcurrentQueue<ModerationAiInput> Inputs { get; } = new();
        public Task<ModerationAiResult> ReviewAsync(ModerationAiInput input, CancellationToken ct)
        {
            Inputs.Enqueue(input);
            return review(input, ct);
        }
    }

    private sealed class FailNotificationInsert : DbCommandInterceptor
    {
        public bool Triggered { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            FailIfNotification(command);
            return ValueTask.FromResult(result);
        }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            FailIfNotification(command);
            return ValueTask.FromResult(result);
        }
        private void FailIfNotification(DbCommand command)
        {
            if (!command.CommandText.Replace("\"", "").Contains("INSERT INTO notifications", StringComparison.OrdinalIgnoreCase)) return;
            Triggered = true;
            throw new InvalidOperationException("Test notification write failure.");
        }
    }
}
