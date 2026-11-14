using System.Security.Claims;
using Wayd.Common.Application.Interfaces;
using Wayd.Infrastructure.Auth;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.UserManagement.Profiles;

namespace Wayd.Web.Api.Controllers.UserManagement;

[Route("api/user-management/profiles")]
[ApiController]
public class ProfileController(IUserService userService, IDispatcher dispatcher, ICurrentUser currentUser) : ControllerBase
{
    private readonly IUserService _userService = userService;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICurrentUser _currentUser = currentUser;

    [HttpGet]
    [EndpointSummary("Get profile details of currently logged in user.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDetailsDto>> Get(CancellationToken cancellationToken)
    {
        return User.GetUserId() is not { } userId || string.IsNullOrEmpty(userId)
            ? Unauthorized()
            : Ok(await _userService.GetAsync(userId, cancellationToken));
    }

    [HttpPut]
    [EndpointSummary("Update profile details of currently logged in user.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(UpdateProfileRequest request)
    {
        if (User.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
            return Unauthorized();

        await _userService.UpdateAsync(request.ToUpdateUserCommand(), userId);
        return NoContent();
    }

    [HttpPut("change-password")]
    [EndpointSummary("Change password for the currently logged in local user.")]
    [EndpointDescription("Ends every session the user has, this one included, web and connected application alike: the user signs in again with the new password.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _userService.ChangePasswordAsync(userId, request.ToChangePasswordCommand(), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("permissions")]
    [EndpointSummary("Get permissions of currently logged in user.")]
    [EndpointDescription("The permissions this request can use: the user's, narrowed by the scope of the token it was made with.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserPermissionsResponse>> GetPermissions(CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
            return Unauthorized();

        var permissions = (await _userService.GetPermissionsAsync(userId, cancellationToken))
            .Where(p => TokenScopeAccess.Permits(User, p))
            .ToList();
        var employeeId = _currentUser.GetEmployeeId();

        return Ok(new UserPermissionsResponse(permissions, employeeId));
    }

    [HttpGet("preferences")]
    [EndpointSummary("Get preferences of currently logged in user.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserPreferencesDto>> GetPreferences(CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
            return Unauthorized();

        return Ok(await _userService.GetPreferences(userId, cancellationToken));
    }

    [HttpPut("preferences")]
    [EndpointSummary("Update preferences of currently logged in user.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdatePreferences(UserPreferencesDto preferences, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _userService.UpdatePreferences(userId, preferences, cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("theme")]
    [EndpointSummary("Get theme configuration of currently logged in user.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserThemeConfigDto?>> GetThemeConfig(CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
            return Unauthorized();

        return Ok(await _userService.GetThemeConfig(userId, cancellationToken));
    }

    [HttpPut("theme")]
    [EndpointSummary("Update theme configuration of currently logged in user.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateThemeConfig(UserThemeConfigDto? themeConfig, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _userService.UpdateThemeConfig(userId, themeConfig, cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("home-layout")]
    [EndpointSummary("Get the home page layout of the currently logged in user.")]
    [EndpointDescription("The widgets the user placed, in order, with their column and whether they show, and the attention tiles the user switched off. No widgets means the user has not arranged the page and sees the default arrangement.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HomeLayoutResponse>> GetHomeLayout(CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
            return Unauthorized();

        return Ok(HomeLayoutResponse.From(await _userService.GetHomeLayout(userId, cancellationToken)));
    }

    [HttpPut("home-layout")]
    [EndpointSummary("Update the home page layout of the currently logged in user.")]
    [EndpointDescription("Replaces the saved layout and leaves the user's other preferences untouched. Send no widgets to return to the default arrangement, and no muted tiles to show every attention tile.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateHomeLayout(UpdateHomeLayoutRequest request, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _userService.UpdateHomeLayout(userId, request.ToUserHomeLayoutDto(), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}