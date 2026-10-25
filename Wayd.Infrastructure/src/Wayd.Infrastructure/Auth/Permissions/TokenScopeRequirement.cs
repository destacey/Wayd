using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Wayd.Infrastructure.Auth.Permissions;

/// <summary>
/// Holds a read-only token (<see cref="TokenScopeAccess.IsReadOnly"/>) to requests that change nothing. A request with
/// a safe method passes, as does one to an action marked <see cref="ReadOnlyOperationAttribute"/>; anything else is
/// refused, whatever permission the endpoint names. Inferring "changes nothing" from a permission's action would not
/// hold: some endpoints change a record under its view permission, letting its requester edit it.
/// </summary>
/// <remarks>
/// A SignalR hub method call is refused too: its resource is the invocation rather than a request, and the hubs serve
/// the web app's live updates, which a token has no use for.
/// </remarks>
internal sealed class TokenScopeRequirement : IAuthorizationRequirement
{
    /// <summary>The one instance every policy shares.</summary>
    public static TokenScopeRequirement Instance { get; } = new();

    private TokenScopeRequirement() { }
}

/// <summary>Decides <see cref="TokenScopeRequirement"/>.</summary>
internal sealed class TokenScopeAuthorizationHandler : AuthorizationHandler<TokenScopeRequirement>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TokenScopeRequirement requirement)
    {
        if (!TokenScopeAccess.IsReadOnly(context.User) || context.Resource is HttpContext httpContext && ChangesNothing(httpContext))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }

    /// <summary>Whether the request is sent with a safe method or to an action marked as changing nothing.</summary>
    private static bool ChangesNothing(HttpContext httpContext)
    {
        var method = httpContext.Request.Method;
        return HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method)
            || httpContext.GetEndpoint()?.Metadata.GetMetadata<ReadOnlyOperationAttribute>() is not null;
    }
}
