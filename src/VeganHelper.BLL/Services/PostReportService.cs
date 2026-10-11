using System.Text.Json;
using AutoMapper;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;
using VeganHelper.BLL.DTOs.Posts;
using VeganHelper.BLL.Exceptions;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class PostReportService(IPostReportRepository reports, IModerationRepository moderation,
    IAdminAuditService audit, INotificationPublisher notifications, IMapper mapper, TimeProvider clock) : IPostReportService
{
    public async Task<PostReportReceipt> ReportAsync(long userId, long postId, CreatePostReportRequest request, CancellationToken ct)
    {
        var category = request.ReasonCategory?.Trim().ToLowerInvariant();
        if (category is not ("non_vegan" or "violence" or "harassment" or "spam" or "other") ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000 || request.ExpectedRevision < 1)
            throw new ArgumentException("Choose a valid reason, provide a description and the viewed content revision.");
        await using var tx = await reports.BeginAsync(ct);
        var reporter = await reports.LockReporterAsync(userId, ct);
        if (reporter is null || !reporter.IsActive) throw new UnauthorizedAccessException("An active account is required.");
        var post = await moderation.LockPostAsync(postId, ct);
        if (post is null || post.IsDeleted || post.Status != "published") throw new NotFoundException("Published post not found.");
        if (post.AuthorId == userId) throw new UnauthorizedAccessException("You cannot report your own post.");
        if (post.ContentRevision != request.ExpectedRevision) throw new ConflictException("Post changed. Reload before reporting.");
        var existing = await reports.PendingAsync(userId, postId, post.ContentRevision, ct);
        if (existing is not null) return new(existing.Id, existing.Status);
        var now = clock.GetUtcNow().UtcDateTime;
        if (await reports.RecentCountAsync(userId, now.AddHours(-1), ct) >= 10) throw new ReportRateLimitException();
        var content = await moderation.DetailAsync(postId, ct) ?? throw new NotFoundException("Post not found.");
        var flag = new Flag { PostId = post.Id, ReporterId = userId, PostRevision = post.ContentRevision, SourceType = "user",
            ReasonCategory = category, Reason = request.Reason.Trim(), Status = "pending", CreatedAt = now,
            ReportedSnapshotJson = JsonSerializer.Serialize(mapper.Map<ModerationPostDetailDto>(content), JsonSerializerOptions.Web) };
        reports.Add(flag);
        await reports.RequestRecheckAsync(post, now, ct);
        await reports.SaveAsync(ct); await tx.CommitAsync(ct);
        return new(flag.Id, flag.Status);
    }
    public Task<PagedResult<AdminPostReportDto>> ListAsync(AdminActor actor, PostReportListRequest request, CancellationToken ct)
    {
        Pagination.Validate(request.PageIndex, request.PageSize);
        var status = request.Status?.Trim().ToLowerInvariant();
        if (status is not ("pending" or "resolved" or "dismissed" or "all") || request.PostId is <= 0)
            throw new ArgumentException("Invalid report filter.");
        return audit.ExecuteAsync(actor, "report.list", "flag", null, async token =>
            Pagination.Map<Flag, AdminPostReportDto>(await reports.ListAsync(status == "all" ? null : status, request.PostId,
                request.PageIndex, request.PageSize, token), request.PageIndex, request.PageSize, mapper), null, ct);
    }
    public Task<AdminPostReportDetailDto> GetAsync(AdminActor actor, long id, CancellationToken ct) =>
        audit.ExecuteAsync(actor, "report.view", "flag", id.ToString(), async token =>
        {
            var report = await reports.FindAsync(id, false, token);
            if (report?.SourceType != "user" || report.PostId is null) throw new NotFoundException("Report not found.");
            var post = await reports.PostAsync(report.PostId.Value, token) ?? throw new NotFoundException("Post not found.");
            var current = mapper.Map<ModerationPostDetailDto>(post);
            current.Flags = mapper.Map<List<ModerationFlagDto>>(await moderation.CurrentFlagsAsync(post.Id, token));
            return new AdminPostReportDetailDto(mapper.Map<AdminPostReportDto>(report), current,
                mapper.Map<List<ModerationAssessmentDto>>(await reports.HistoryAsync(post.Id, token)));
        }, null, ct);
    public Task<AdminPostReportDto> DecideAsync(AdminActor actor, long id, ModerationDecisionRequest request, CancellationToken ct)
    {
        var action = request.Action?.Trim().ToLowerInvariant();
        if (action is not ("dismiss" or "keep" or "remove") || string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Length > 1000 || request.ExpectedRevision < 1) throw new ArgumentException("Invalid report decision.");
        return audit.ExecuteAsync(actor, "report." + action, "flag", id.ToString(), async token =>
        {
            var preview = await reports.FindAsync(id, false, token);
            if (preview?.SourceType != "user" || preview.PostId is null) throw new NotFoundException("Report not found.");
            var post = await moderation.LockPostAsync(preview.PostId.Value, token);
            if (post is null) throw new NotFoundException("Post not found.");
            if (post.IsDeleted && action != "dismiss") throw new ConflictException("Deleted posts cannot be restored or hidden. Dismiss the report to close it.");
            var report = await reports.FindAsync(id, true, token);
            if (report?.Status != "pending") throw new ConflictException("Report already reviewed.");
            if (post.ContentRevision != request.ExpectedRevision) throw new ConflictException("Post changed. Reload before reviewing.");
            var now = clock.GetUtcNow().UtcDateTime; var reason = request.Reason.Trim();
            if (action == "dismiss")
            {
                report.Status = "dismissed"; report.ResolvedByAdminId = actor.UserId; report.ResolvedAt = now; report.ResolutionNote = reason;
                await reports.SaveAsync(token);
            }
            else
            {
                var keep = action == "keep";
                post.Status = keep ? "published" : "hidden"; post.ModerationHold = !keep; post.UpdatedAt = now;
                await moderation.ResolveFlagsAsync(post.Id, post.ContentRevision, actor.UserId, reason, keep, now, token);
                await moderation.ObsoleteScansAsync(post.Id, post.ContentRevision, now, token);
                var decision = new PostModerationDecision { PostId = post.Id, Revision = post.ContentRevision, Action = action!,
                    ActorType = "admin", AdminId = actor.UserId, FlagId = id, Reason = reason, CreatedAt = now };
                moderation.AddDecision(decision); await moderation.SaveAsync(token);
                await notifications.NotifyModerationDecisionAsync(decision.Id, token);
                // ExecuteUpdate does not refresh the tracked report.
                report.Status = keep ? "dismissed" : "resolved"; report.ResolvedByAdminId = actor.UserId;
                report.ResolvedAt = now; report.ResolutionNote = reason;
            }
            return mapper.Map<AdminPostReportDto>(report);
        }, null, ct);
    }
}
