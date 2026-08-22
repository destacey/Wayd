using Microsoft.AspNetCore.Authorization;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Identity.Bootstrap;
using Wayd.Common.Application.Identity.Tokens;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.UserManagement.Users;

namespace Wayd.Web.Api.Controllers.UserManagement;

[Route("api/auth")]
[ApiVersionNeutral]
[ApiController]
// AllowAnonymous is applied per action, not to the controller: a controller-level
// AllowAnonymous cannot be narrowed by an action-level Authorize (ASP0026), which would
// silently leave the authenticated logout endpoint open.
public class AuthController(
    ITokenService tokenService,
    IBootstrapTokenService bootstrapTokenService,
    IUserService userService,
    ICurrentUser currentUser) : ControllerBase
{
    private readonly ITokenService _tokenService = tokenService;
    private readonly IBootstrapTokenService _bootstrapTokenService = bootstrapTokenService;
    private readonly IUserService _userService = userService;
    private readonly ICurrentUser _currentUser = currentUser;

    [AllowAnonymous]
    [HttpPost("login")]
    [OpenApiOperation("Authenticate with username and password.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> Login(LoginCommand command, CancellationToken cancellationToken)
    {
        var response = await _tokenService.GetTokenAsync(command, cancellationToken);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("refresh-token")]
    [OpenApiOperation("Refresh an expired JWT token.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> RefreshToken(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var response = await _tokenService.RefreshTokenAsync(command, cancellationToken);
        return Ok(response);
    }

    [Authorize]
    [HttpPost("logout")]
    [OpenApiOperation("End the calling device's session server-side.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(LogoutRequest? request, CancellationToken cancellationToken)
    {
        // The subject comes from the validated principal, so this can only ever act on the
        // caller's own account. The refresh token in the body selects which of their sessions
        // to end; it cannot reach another user's, because the store scopes by user id too.
        await _tokenService.LogoutAsync(_currentUser.GetUserId(), request?.RefreshToken, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("logout-all")]
    [OpenApiOperation("End every session for the calling user, on all devices.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        await _tokenService.LogoutAllAsync(_currentUser.GetUserId(), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("sessions")]
    [OpenApiOperation("List the calling user's active sessions.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<UserSessionResponse>>> GetSessions(
        LogoutRequest? request,
        CancellationToken cancellationToken)
    {
        // POST rather than GET: the caller's refresh token identifies which row is "this
        // device", and a credential belongs in a body, not a query string where it would land
        // in access logs and browser history.
        var sessions = await _tokenService.GetSessions(_currentUser.GetUserId(), request?.RefreshToken, cancellationToken);
        return Ok(sessions);
    }

    [Authorize]
    [HttpDelete("sessions/{sessionId:guid}")]
    [OpenApiOperation("Revoke one of the calling user's sessions.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var revoked = await _tokenService.RevokeSession(_currentUser.GetUserId(), sessionId, cancellationToken);

        // 404 covers "already revoked", "expired" and "belongs to someone else" alike, so the
        // response never confirms that another user's session id exists.
        return revoked ? NoContent() : NotFound();
    }

    [AllowAnonymous]
    [HttpPost("exchange")]
    [OpenApiOperation("Exchange an external identity-provider token (e.g., Microsoft Entra ID) for a Wayd JWT.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> Exchange(ExchangeTokenCommand command, CancellationToken cancellationToken)
    {
        var response = await _tokenService.ExchangeTokenAsync(command, cancellationToken);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpGet("providers")]
    [OpenApiOperation("List the authentication providers enabled on this deployment.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthProvidersResponse>> GetProviders(CancellationToken cancellationToken)
    {
        // Anonymous and cheap — the frontend calls this before constructing any
        // OIDC client. The response contains only public OIDC client metadata
        // (Authority, ClientId, Scopes) per provider; AllowedTenantIds and any
        // future secrets are deliberately not exposed here.
        var response = await _tokenService.GetAuthProviders(cancellationToken);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("setup")]
    [OpenApiOperation("Create the first admin user using the one-time bootstrap token.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TokenResponse>> Setup(SetupRequest request, CancellationToken cancellationToken)
    {
        if (!_bootstrapTokenService.IsActive)
            return Conflict(ProblemDetailsExtensions.ForConflict("Setup has already been completed.", HttpContext));

        if (!_bootstrapTokenService.Validate(request.Token))
            return BadRequest(ProblemDetailsExtensions.ForBadRequest("Invalid setup token.", HttpContext));

        // Double-check that no users exist — prevents a race where two concurrent
        // setup requests both pass the token check before one consumes it.
        var userCount = await _userService.GetCountAsync(cancellationToken);
        if (userCount > 0)
            return Conflict(ProblemDetailsExtensions.ForConflict("Setup has already been completed.", HttpContext));

        var createResult = await _userService.CreateAsync(new CreateUserCommand
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            LoginProvider = LoginProviders.Wayd,
            Password = request.Password,
            MustChangePassword = false,
        }, cancellationToken);

        if (createResult.IsFailure)
            return BadRequest(createResult.ToBadRequestObject(HttpContext));

        var userId = createResult.Value;

        await _userService.AssignRolesAsync(
            new AssignUserRolesCommand(userId, [ApplicationRoles.Admin]),
            cancellationToken);

        // Consume the token only after successful user creation so a failed
        // attempt (e.g. validation error, duplicate email) doesn't force the
        // operator to restart the application to get a new token.
        _bootstrapTokenService.Consume();

        var token = await _tokenService.GetTokenAsync(
            new LoginCommand(request.Email, request.Password),
            cancellationToken);

        return Ok(token);
    }
}
