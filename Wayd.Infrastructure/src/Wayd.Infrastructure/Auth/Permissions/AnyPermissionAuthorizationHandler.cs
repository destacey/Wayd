using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Wayd.Infrastructure.OpenTelemetry;

namespace Wayd.Infrastructure.Auth.Permissions;

internal class AnyPermissionAuthorizationHandler(IUserService userService) : AuthorizationHandler<AnyPermissionRequirement>
{
    private readonly IUserService _userService = userService;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AnyPermissionRequirement requirement)
    {
        using var activity = AuthTelemetry.StartCheck(string.Join(',', requirement.Permissions));

        if (context.User?.GetUserId() is not { } userId)
        {
            AuthTelemetry.SetResult(activity, false);
            return;
        }

        foreach (var permission in requirement.Permissions)
        {
            if (await _userService.HasPermissionAsync(userId, permission))
            {
                context.Succeed(requirement);
                AuthTelemetry.SetResult(activity, true);
                return;
            }
        }

        AuthTelemetry.SetResult(activity, false);
    }
}
