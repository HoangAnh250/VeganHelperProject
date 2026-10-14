using Microsoft.AspNetCore.Mvc;
using VeganHelper.BLL.Contracts;

namespace VeganHelper.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CategoriesController(ICategoryService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken) =>
        Ok(await service.GetPostCategoriesAsync(cancellationToken));
}