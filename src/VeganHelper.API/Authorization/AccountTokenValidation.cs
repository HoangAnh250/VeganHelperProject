using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using VeganHelper.BLL.Contracts;

namespace VeganHelper.API.Authorization;

internal static class AccountTokenValidation
{
    internal static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (!long.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal?.FindFirstValue("sub"),
                NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId <= 0
            || !int.TryParse(principal?.FindFirstValue("token_version"), NumberStyles.None, CultureInfo.InvariantCulture, out var version))
        {
            context.Fail("Invalid session. Sign in again.");
            return;
        }
        var role = await context.HttpContext.RequestServices.GetRequiredService<IAccountAccessService>()
            .GetSessionRoleAsync(userId, version, context.HttpContext.RequestAborted);
        if (role is null)
        {
            context.Fail("Session has been revoked or account is unavailable.");
            return;
        }
        // Use the current role even on APIs that authorize directly with role claims.
        var identity = (ClaimsIdentity)principal!.Identity!;
        foreach (var claim in identity.FindAll(identity.RoleClaimType).ToArray()) identity.RemoveClaim(claim);
        identity.AddClaim(new Claim(identity.RoleClaimType, role));
    }
}
