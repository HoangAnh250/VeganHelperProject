namespace VeganHelper.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    [HttpGet("generate-token")]
    [AllowAnonymous]
    public IActionResult GenerateToken()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, "author"),
            new Claim(ClaimTypes.Name, "TestUser")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("this_is_a_very_long_secret_key_for_testing_purposes_only_123456789"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "VeganHelper",
            audience: "VeganHelperUsers",
            claims: claims,
            expires: DateTime.Now.AddDays(1),
            signingCredentials: creds
        );

        return Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token)
        });
    }

    [HttpGet("seed-database")]
    [AllowAnonymous]
    public async System.Threading.Tasks.Task<IActionResult> SeedDatabase([FromServices] VeganHelper.DAL.Persistence.AppDbContext dbContext)
    {
        if (!dbContext.Roles.Any(r => r.Id == 1))
        {
            dbContext.Database.ExecuteSqlRaw("SET IDENTITY_INSERT roles ON; INSERT INTO roles (id, role_name) VALUES (1, 'member'); SET IDENTITY_INSERT roles OFF;");
        }
        
        if (!dbContext.Users.Any(u => u.Id == 1))
        {
            dbContext.Database.ExecuteSqlRaw("SET IDENTITY_INSERT users ON; INSERT INTO users (id, username, email, role_id) VALUES (1, 'testuser', 'test@example.com', 1); SET IDENTITY_INSERT users OFF;");
        dbContext.UserProfiles.Add(new VeganHelper.DAL.Entities.UserProfile { UserId = 1, DisplayName = "Gordon Ramsay (Vegan)" });
        }

        if (!dbContext.Categories.Any(c => c.Id == 1))
        {
            dbContext.Database.ExecuteSqlRaw("SET IDENTITY_INSERT categories ON; INSERT INTO categories (id, name, slug, category_type, post_category_kind) VALUES (1, 'Vegan Food', 'vegan-food', 'post', 'recipe'); SET IDENTITY_INSERT categories OFF;");
        }

        if (!dbContext.Ingredients.Any(i => i.Id == 1))
        {
            dbContext.Database.ExecuteSqlRaw("SET IDENTITY_INSERT ingredients ON; INSERT INTO ingredients (id, name, default_unit) VALUES (1, 'Tofu', 'g'); SET IDENTITY_INSERT ingredients OFF;");
        }

        await dbContext.SaveChangesAsync();
        return Ok("Database seeded successfully with User ID 1, Category ID 1, and Ingredient ID 1.");
    }
}
