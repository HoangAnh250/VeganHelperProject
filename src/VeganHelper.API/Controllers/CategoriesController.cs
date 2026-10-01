using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Persistence;
using VeganHelper.DAL.Entities;

namespace VeganHelper.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _context;

    public CategoriesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _context.Categories
            .Where(c => c.IsActive && c.CategoryType == "Post")
            .Select(c => new { 
                id = c.Id, 
                name = c.Name, 
                slug = c.Slug, 
                postCategoryKind = c.PostCategoryKind 
            })
            .ToListAsync();

        if (categories.Count == 0)
        {
            // Fallback in case they didn't set CategoryType properly
            categories = await _context.Categories
                .Where(c => c.IsActive)
                .Select(c => new { 
                    id = c.Id, 
                    name = c.Name, 
                    slug = c.Slug, 
                    postCategoryKind = c.PostCategoryKind 
                })
                .ToListAsync();
        }

        return Ok(categories);
    }
}
