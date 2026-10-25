using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Wayd.Infrastructure.OpenTelemetry;

namespace Wayd.Infrastructure.Auth.Permissions;

internal class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IUserService _userService;

    public PermissionAuthorizationHandler(IUserService userService) =>
        _userService = userService;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        using var activity = AuthTelemetry.StartCheck(requirement.Permission);

        var allowed = context.User?.GetUserId() is { } userId
            && TokenScopeAccess.Permits(context.User, requirement.Permission)
            && await _userService.HasPermissionAsync(userId, requirement.Permission);

        if (allowed)
            context.Succeed(requirement);

        AuthTelemetry.SetResult(activity, allowed);
    }
}