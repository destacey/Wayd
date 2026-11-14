using Microsoft.AspNetCore.Authentication;
using Wayd.Infrastructure;

namespace Wayd.Web.Api.Extensions;

/// <summary>
/// Whether a request was authorized as a sign-in to the web app, rather than a personal access token or a hosted MCP
/// tool call's delegated token, which the controllers' default policy also accepts.
/// </summary>
public static class WebAppSession
{
    /// <summary>
    /// Whether <paramref name="context"/>'s principal came from a sign-in to the web app.
    /// </summary>
    /// <remarks>
    /// Read from the ticket the authorization middleware recorded, never by authenticating again: a request carrying a
    /// personal access token and another user's web app token would otherwise pass as the second user while being
    /// bound to the first. The scheme must equal <c>LocalJwt</c> alone, so authorization metadata naming a second
    /// scheme on an endpoint would refuse every caller rather than admit one.
    /// </remarks>
    public static bool IsCurrent(HttpContext context) =>
        context.Features.Get<IAuthenticateResultFeature>()?.AuthenticateResult?.Ticket is { } ticket
        && ticket.AuthenticationScheme == AuthConstants.LocalJwtScheme
        && ReferenceEquals(ticket.Principal, context.User);
}
