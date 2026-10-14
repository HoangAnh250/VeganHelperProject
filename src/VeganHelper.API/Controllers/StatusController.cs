using Microsoft.AspNetCore.Mvc;
using VeganHelper.BLL.Contracts;
namespace VeganHelper.API.Controllers;
[ApiController]
[Route("api/status")]
public sealed class StatusController(IStatusService service, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment()) return NotFound();
        var status = await service.GetAsync(cancellationToken);
        return StatusCode(status.DatabaseAvailable ? 200 : 503, status);
    }
}
