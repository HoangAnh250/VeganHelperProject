using System.Globalization;
using AutoMapper;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;
using VeganHelper.BLL.Exceptions;
using VeganHelper.BLL.Mapping;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class AdminModerationService(IModerationRepository repository, IAdminAuditService audit,
    INotificationPublisher notifications, IMapper mapper, TimeProvider clock) : IAdminModerationService
{
    public Task<PagedResult<ModerationPostDto>> ListAsync(AdminActor actor, ModerationPostListRequest request, CancellationToken ct)
    {
        Pagination.Validate(request.PageIndex, request.PageSize);
        if (request.Keyword?.Length > 100) throw new ArgumentException("Keyword must be at most 100 characters.");
        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim();
        var status = request.Status?.Trim().ToLowerInvariant();
        if (status is not ("pending_review" or "published" or "rejected" or "hidden" or "draft" or "all")) throw new ArgumentException("Invalid post status.");
        return audit.ExecuteAsync(actor, "moderation.list", "post", null, async token =>
            Pagination.Map<ModerationPostProjection, ModerationPostDto>(await repository.ListPostsAsync(keyword, status == "all" ? null : status,
                request.PageIndex, request.PageSize, token), request.PageIndex, request.PageSize, mapper), null, ct);
    }
    public Task<ModerationPostDetailDto> GetAsync(AdminActor actor, long id, CancellationToken ct) =>
        audit.ExecuteAsync(actor, "moderation.view", "post", Target(id), async token =>
        {
            var post = await repository.DetailAsync(id, token) ?? throw new NotFoundException("Post not found.");
            var result = mapper.Map<ModerationPostDetailDto>(post);
            var scan = await repository.CurrentScanAsync(id, post.ContentRevision, token);
            result.ScanState = scan?.State; result.ConfidenceScore = scan?.Confidence;
            result.ScanErrorCode = scan?.ErrorCode; result.ScanSummary = scan?.Summary;
            result.Findings = ModerationMapping.Findings(scan?.FindingsJson ?? "[]");
            result.Flags = mapper.Map<List<ModerationFlagDto>>(await repository.CurrentFlagsAsync(id, token));
            result.Decisions = mapper.Map<List<ModerationDecisionDto>>(await repository.DecisionsAsync(id, token));
            return result;
        }, null, ct);
    public Task<PagedResult<ModerationFlagDto>> FlagsAsync(AdminActor actor, ModerationFlagListRequest request, CancellationToken ct)
    {
        Pagination.Validate(request.PageIndex, request.PageSize);
        if (request.PostId <= 0 || request.MinConfidence.HasValue && (!double.IsFinite(request.MinConfidence.Value) || request.MinConfidence < 0 || request.MinConfidence > 1))
            throw new ArgumentException("Invalid flag filter.");
        return audit.ExecuteAsync(actor, "ai_flag.list", "flag", null, async token =>
            Pagination.Map<ModerationFlagProjection, ModerationFlagDto>(await repository.ListFlagsAsync(request.PostId, request.MinConfidence,
                request.PageIndex, request.PageSize, token), request.PageIndex, request.PageSize, mapper), null, ct);
    }
    public Task<ModerationDecisionDto> DecideAsync(AdminActor actor, long postId, ModerationDecisionRequest request, CancellationToken ct) =>
        DecideCoreAsync(actor, postId, null, request, ct);
    public async Task<ModerationDecisionDto> DecideFlagAsync(AdminActor actor, long flagId, ModerationDecisionRequest request, CancellationToken ct)
    {
        if (!await audit.IsActiveAdminAsync(actor.UserId, ct)) throw new UnauthorizedAccessException("Admin access is required.");
        var flag = await repository.FindFlagAsync(flagId, ct);
        if (flag?.SourceType != "ai" || flag.PostId is null) throw new NotFoundException("AI flag not found.");
        return await DecideCoreAsync(actor, flag.PostId.Value, flagId, request, ct);
    }
    private Task<ModerationDecisionDto> DecideCoreAsync(AdminActor actor, long postId, long? flagId, ModerationDecisionRequest request, CancellationToken ct)
    {
        var action = request.Action?.Trim().ToLowerInvariant();
        if (flagId.HasValue ? action is not ("keep" or "remove") : action is not ("approve" or "reject")) throw new ArgumentException("Invalid moderation action.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000 || request.ExpectedRevision < 1)
            throw new ArgumentException("Reason and a positive expected revision are required.");
        var reason = request.Reason.Trim();
        return audit.ExecuteAsync(actor, flagId.HasValue ? $"ai_flag.{action}" : $"post.{action}", flagId.HasValue ? "flag" : "post",
            Target(flagId ?? postId), async token =>
        {
            var post = await repository.LockPostAsync(postId, token);
            if (post is null || post.IsDeleted) throw new NotFoundException("Post not found.");
            if (post.ContentRevision != request.ExpectedRevision) throw new ConflictException("Post content has changed. Reload before reviewing.");
            if (flagId.HasValue)
            {
                var flag = await repository.FindFlagAsync(flagId.Value, token);
                if (flag is null || flag.PostId != postId || flag.SourceType != "ai" || flag.Status != "pending" ||
                    flag.PostRevision.HasValue && flag.PostRevision != post.ContentRevision || post.Status is not ("pending_review" or "published"))
                    throw new ConflictException("This flag is no longer available for review.");
            }
            else if (post.Status != "pending_review") throw new ConflictException("This post has already been reviewed.");
            var now = clock.GetUtcNow().UtcDateTime;
            var keep = action is "approve" or "keep";
            post.Status = keep ? "published" : action == "reject" ? "rejected" : "hidden";
            post.UpdatedAt = now;
            await repository.ResolveFlagsAsync(postId, post.ContentRevision, actor.UserId, reason, keep, now, token);
            await repository.ObsoleteScansAsync(postId, post.ContentRevision, now, token);
            var decision = new PostModerationDecision { PostId = postId, Revision = post.ContentRevision, Action = action!, ActorType = "admin",
                AdminId = actor.UserId, FlagId = flagId, Reason = reason, CreatedAt = now };
            repository.AddDecision(decision);
            await repository.SaveAsync(token);
            await notifications.NotifyModerationDecisionAsync(decision.Id, token);
            return mapper.Map<ModerationDecisionDto>(decision);
        }, null, ct);
    }
    public Task<ModerationSettingsDto> SettingsAsync(AdminActor actor, CancellationToken ct) =>
        audit.ExecuteAsync(actor, "moderation.settings.view", "moderation_settings", "1", async token =>
            mapper.Map<ModerationSettingsDto>(await repository.SettingsAsync(null, token)), null, ct);
    public Task<ModerationSettingsDto> UpdateSettingsAsync(AdminActor actor, ModerationSettingsRequest request, CancellationToken ct) =>
        audit.ExecuteAsync(actor, "moderation.settings.update", "moderation_settings", "1", async token =>
        {
            var settings = await repository.SettingsAsync("update", token);
            if (request.ExpectedVersion < 1) throw new ArgumentException("A positive expected version is required.");
            if (settings.Version != request.ExpectedVersion) throw new ConflictException("Moderation settings have changed. Reload before updating.");
            settings.AutoPublishEnabled = request.AutoPublishEnabled; settings.Version = checked(settings.Version + 1);
            settings.UpdatedByAdminId = actor.UserId; settings.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            await repository.SaveAsync(token);
            return mapper.Map<ModerationSettingsDto>(settings);
        }, null, ct);
    private static string Target(long id) => id > 0 ? id.ToString(CultureInfo.InvariantCulture) : throw new ArgumentException("Invalid ID.");
}
