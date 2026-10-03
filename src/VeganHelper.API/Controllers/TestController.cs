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
    public IActionResult SeedDatabase()
    {
        return StatusCode(StatusCodes.Status410Gone, new { message = "Use the explicit --seed CLI command. HTTP requests cannot seed the shared database." });
    }
}
