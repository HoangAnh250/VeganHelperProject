using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs.Posts;

namespace VeganHelper.API.Controllers;

[ApiController, Authorize, Route("api/posts/{id:long:min(1)}/reports")]
public sealed class PostReportsController(IPostReportService service) : ControllerBase
{
    [HttpPost]
    public Task<PostReportReceipt> Report(long id, CreatePostReportRequest request, CancellationToken ct) =>
        service.ReportAsync(long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), id, request, ct);
}
