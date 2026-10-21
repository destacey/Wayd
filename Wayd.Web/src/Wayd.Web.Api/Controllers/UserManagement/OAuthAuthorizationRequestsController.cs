using Microsoft.AspNetCore.Authentication;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Identity;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Infrastructure;
using Wayd.Infrastructure.Auth.OAuth;
using Wayd.Web.Api.Models.UserManagement.OAuth;

namespace Wayd.Web.Api.Controllers.UserManagement;

/// <summary>
/// The consent step of Wayd's OAuth authorization server: the web app reads a pending request by the handle the
/// authorization endpoint gave it, and the signed-in user approves or denies it.
/// </summary>
/// <remarks>
/// Any user signed in to the web app may answer a request, which binds it to them; the handle is unguessable, and a
/// request reaches a user only when an application sends them to it. A personal access token is refused: approving
/// would turn a short-lived or revocable token into a connector session that outlives it.
/// </remarks>
[Route("api/oauth/authorization-requests")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.McpServer)]
public class OAuthAuthorizationRequestsController(IAuthorizationServer authorizationServer, ICurrentUser currentUser) : ControllerBase
{
    private readonly IAuthorizationServer _authorizationServer = authorizationServer;
    private readonly ICurrentUser _currentUser = currentUser;

    /// <summary>Gets a connection request waiting for the user's decision.</summary>
    /// <param name="handle">The handle the authorization endpoint passed to the consent page as `request`.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpGet("{handle}")]
    [EndpointSummary("Get a pending OAuth connection request.")]
    [EndpointDescription("Answers 404 when the request is unknown, has expired, or has already been approved or denied, and 403 to a personal access token: only a user signed in to the web app may answer a request.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OAuthAuthorizationRequestResponse>> Get(string handle, CancellationToken cancellationToken)
    {
        if (!await IsWebAppSession())
            return WebAppSessionRequired();

        var pending = await _authorizationServer.FindPending(handle, cancellationToken);
        return pending.IsSuccess ? Ok(OAuthAuthorizationRequestResponse.From(pending.Value)) : NotFound();
    }

    /// <summary>Approves a connection request for the signed-in user.</summary>
    /// <param name="handle">The handle the authorization endpoint passed to the consent page as `request`.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpPost("{handle}/approve")]
    [EndpointSummary("Approve a pending OAuth connection request.")]
    [EndpointDescription("Issues a single-use authorization code bound to the signed-in user and answers the URL to send the browser to. Answers 404 as the get does.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OAuthAuthorizationDecisionResponse>> Approve(string handle, CancellationToken cancellationToken)
    {
        if (!await IsWebAppSession())
            return WebAppSessionRequired();

        var redirect = await _authorizationServer.Approve(handle, _currentUser.GetUserId(), cancellationToken);
        return redirect.IsSuccess ? Ok(new OAuthAuthorizationDecisionResponse { RedirectUrl = redirect.Value }) : NotFound();
    }

    /// <summary>Denies a connection request.</summary>
    /// <param name="handle">The handle the authorization endpoint passed to the consent page as `request`.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpPost("{handle}/deny")]
    [EndpointSummary("Deny a pending OAuth connection request.")]
    [EndpointDescription("Answers the URL that tells the application it was refused. Answers 404 as the get does.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OAuthAuthorizationDecisionResponse>> Deny(string handle, CancellationToken cancellationToken)
    {
        if (!await IsWebAppSession())
            return WebAppSessionRequired();

        var redirect = await _authorizationServer.Deny(handle, _currentUser.GetUserId(), cancellationToken);
        return redirect.IsSuccess ? Ok(new OAuthAuthorizationDecisionResponse { RedirectUrl = redirect.Value }) : NotFound();
    }

    /// <summary>
    /// Whether the caller signed in to the web app, rather than authenticating with a personal access token, which the
    /// controllers' default policy also accepts.
    /// </summary>
    private async Task<bool> IsWebAppSession() =>
        (await HttpContext.AuthenticateAsync(AuthConstants.LocalJwtScheme)).Succeeded;

    /// <summary>The 403 for a caller that is not signed in to the web app.</summary>
    private ObjectResult WebAppSessionRequired() =>
        Problem(statusCode: StatusCodes.Status403Forbidden, detail: "Only a user signed in to Wayd can answer a connection request.");
}
