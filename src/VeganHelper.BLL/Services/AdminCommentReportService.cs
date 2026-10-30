using System.Globalization;
using AutoMapper;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;
using VeganHelper.BLL.Exceptions;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class AdminCommentReportService(IAdminCommentReportRepository repository, IAdminAuditService audit,
    IMapper mapper, TimeProvider clock) : IAdminCommentReportService
{
    public Task<PagedResult<AdminCommentReportDto>> ListAsync(AdminActor actor, AdminCommentReportListRequest request, CancellationToken ct)
    {
        Pagination.Validate(request.PageIndex, request.PageSize);
        var status = request.Status?.Trim().ToLowerInvariant();
        if (status is not ("pending" or "resolved" or "dismissed" or "all"))
            throw new ArgumentException("Invalid comment report status.");
        return audit.ExecuteAsync(actor, "comment_report.list", "comment", null, async token =>
            Pagination.Map<AdminCommentReportProjection, AdminCommentReportDto>(
                await repository.ListAsync(status == "all" ? null : status, request.PageIndex, request.PageSize, token),
                request.PageIndex, request.PageSize, mapper), null, ct);
    }

    public Task<AdminCommentReportDto> GetAsync(AdminActor actor, long id, CancellationToken ct) =>
        audit.ExecuteAsync(actor, "comment_report.view", "comment", Target(id), async token =>
            mapper.Map<AdminCommentReportDto>(await repository.FindAsync(id, token)
                ?? throw new NotFoundException("Comment report not found.")), null, ct);

    public Task<AdminCommentReportDto> DecideAsync(AdminActor actor, long id, AdminCommentReportDecisionRequest request, CancellationToken ct)
    {
        var action = request.Action?.Trim().ToLowerInvariant();
        if (action is not ("dismiss" or "keep" or "remove") || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
            throw new ArgumentException("Choose dismiss, keep or remove and provide a reason.");
        var reason = request.Reason.Trim();
        return audit.ExecuteAsync(actor, $"comment_report.{action}", "comment", Target(id), async token =>
        {
            var report = await repository.LockReportAsync(id, token);
            if (report is null || report.SourceType is not ("user" or "keyword") || report.CommentId is null)
                throw new NotFoundException("Comment report not found.");
            if (report.Status != "pending") throw new ConflictException("Comment report already reviewed.");
            var comment = await repository.LockCommentAsync(report.CommentId.Value, token)
                ?? throw new NotFoundException("Reported comment not found.");
            if (comment.IsDeleted && action != "dismiss")
                throw new ConflictException("Deleted comments can only have their remaining reports dismissed.");
            if (!comment.IsDeleted && action == "dismiss" && comment.Status == "hidden")
                throw new ConflictException("Choose keep to restore a hidden comment or remove to delete it.");

            var now = clock.GetUtcNow().UtcDateTime;
            if (action == "keep")
            {
                comment.Status = "visible";
                comment.UpdatedAt = now;
            }
            else if (action == "remove")
            {
                comment.Status = "hidden";
                comment.IsDeleted = true;
                comment.DeletedAt = now;
                comment.UpdatedAt = now;
            }

            await repository.ResolvePendingAsync(comment.Id, action == "remove" ? "resolved" : "dismissed",
                actor.UserId, reason, now, token);
            await repository.SaveAsync(token);

            var original = await repository.FindAsync(id, token);
            return original is null
                ? throw new NotFoundException("Comment report not found.")
                : mapper.Map<AdminCommentReportDto>(original);
        }, null, ct);
    }

    private static string Target(long id) => id > 0
        ? id.ToString(CultureInfo.InvariantCulture)
        : throw new ArgumentException("Invalid comment report ID.");
}
