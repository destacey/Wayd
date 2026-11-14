using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wayd.Common.Application.Identity;
using Wayd.Infrastructure.Identity;

namespace Wayd.Infrastructure.Auth.Permissions;

/// <summary>
/// Holds the token of a user who must change their password (<see cref="ClaimType"/>) to the actions marked
/// <see cref="AllowedDuringPasswordChangeAttribute"/>: changing the password and signing out. Every other request is
/// refused, whatever permission the endpoint names, so a temporary password can do nothing but be replaced.
/// </summary>
/// <remarks>
/// The web app's token and a personal access token carry the claim alike, so a token minted before an administrator
/// reset the password is held to the same until the user replaces it. A connected application's session ends at the
/// reset. A SignalR hub method call is refused too: its resource is the invocation rather than a request, and nothing
/// on a hub replaces a password.
/// </remarks>
public sealed class PasswordChangeRequirement : IAuthorizationRequirement
{
    /// <summary>The web access token claim, <c>true</c>, of a user who must change their password before anything else.</summary>
    public const string ClaimType = "pwd_change";

    /// <summary>The one instance every policy shares.</summary>
    public static PasswordChangeRequirement Instance { get; } = new();

    /// <summary>
    /// Whether <paramref name="user"/>'s tokens carry <see cref="ClaimType"/>: they must replace a temporary password,
    /// which only a local account has. A flag left on an account that has since moved to an identity provider, with no
    /// password to change, restricts nothing.
    /// </summary>
    internal static bool AppliesTo(ApplicationUser user) =>
        user.MustChangePassword && user.LoginProvider == LoginProviders.Wayd;

    private PasswordChangeRequirement() { }
}

/// <summary>Decides <see cref="PasswordChangeRequirement"/>.</summary>
internal sealed class PasswordChangeAuthorizationHandler : AuthorizationHandler<PasswordChangeRequirement>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PasswordChangeRequirement requirement)
    {
        var mustChange = context.User.HasClaim(PasswordChangeRequirement.ClaimType, "true");
        if (!mustChange
            || context.Resource is HttpContext httpContext
                && httpContext.GetEndpoint()?.Metadata.GetMetadata<AllowedDuringPasswordChangeAttribute>() is not null)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Marks an action a user who must change their password may still call (<see cref="PasswordChangeRequirement"/>):
/// changing the password, and signing out instead.
/// </summary>
/// <remarks>Put it on nothing else: it is the whole of what a temporary password can reach.</remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AllowedDuringPasswordChangeAttribute : Attribute;
