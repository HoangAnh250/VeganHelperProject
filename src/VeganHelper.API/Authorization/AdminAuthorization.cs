using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using VeganHelper.BLL.Contracts;

namespace VeganHelper.API.Authorization;

public static class AdminPolicies
{
    public const string AdminOnly = "AdminOnly";
}

public sealed class ActiveAdminRequirement : IAuthorizationRequirement { }

public sealed class ActiveAdminHandler(IAdminAuditService service) : AuthorizationHandler<ActiveAdminRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveAdminRequirement requirement)
    {
        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
        if (context.User.Identity?.IsAuthenticated != true ||
            !long.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId <= 0) return;
        var ct = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        if (await service.IsActiveAdminAsync(userId, ct)) context.Succeed(requirement);
    }
}
