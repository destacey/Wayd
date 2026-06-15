using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.UserManagement.Users;

namespace Wayd.Web.Api.Controllers.UserManagement;

[Route("api/user-management/users")]
[ApiVersionNeutral]
[ApiController]
public class UsersController(IUserService userService) : ControllerBase
{
    private readonly IUserService _userService = userService;

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Users)]
    [OpenApiOperation("Create a new user.", "")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<string>> CreateUser(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _userService.CreateAsync(request.ToCreateUserCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetUser), new { id = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Users)]
    [OpenApiOperation("Get list of all users.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<List<UserDetailsDto>> GetUsers(CancellationToken cancellationToken)
    {
        return await _userService.GetListAsync(cancellationToken);
    }

    [HttpGet("{id}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Users)]
    [OpenApiOperation("Get a user's details.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDetailsDto>> GetUser(string id, CancellationToken cancellationToken)
    {
        var user = await _userService.GetAsync(id, cancellationToken);

        return user is null
            ? NotFound()
            : user;
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Users)]
    [OpenApiOperation("Update a user's details.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateUser(string id, UpdateUserRequest request)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.Id), HttpContext));

        await _userService.UpdateAsync(request.ToUpdateUserCommand(), id);
        return NoContent();
    }

    [HttpGet("{id}/roles")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.UserRoles)]
    [OpenApiOperation("Get a user's roles.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<List<UserRoleDto>> GetRoles(string id, CancellationToken cancellationToken, [FromQuery] bool includeUnassigned = false)
    {
        return await _userService.GetRolesAsync(id, includeUnassigned, cancellationToken);
    }

    [HttpPost("{id}/roles")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.UserRoles)]
    [OpenApiOperation("Update a user's assigned roles.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ManageUserRoles(string id, AssignUserRolesRequest request, CancellationToken cancellationToken)
    {
        if (id != request.UserId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.UserId), HttpContext));

        var result = await _userService.AssignRolesAsync(request.ToAssignUserRolesRequest(), cancellationToken);

        return result.IsSuccess
            ? Ok()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("manage-role")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.UserRoles)]
    [OpenApiOperation("Add or remove users from a role.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ManageRoleUsers(ManageRoleUsersRequest request, CancellationToken cancellationToken)
    {
        var result = await _userService.ManageRoleUsersAsync(request.ToManageRoleUsersCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/reset-password")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Users)]
    [OpenApiOperation("Reset a local user's password.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ResetPassword(string id, ResetPasswordRequest request)
    {
        var result = await _userService.ResetPasswordAsync(new ResetPasswordCommand(id, request.NewPassword));
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/unlock")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Users)]
    [OpenApiOperation("Unlock a locked user account.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UnlockUser(string id)
    {
        var result = await _userService.UnlockUserAsync(id);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/activate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Users)]
    [OpenApiOperation("Activate a user account.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ActivateUser(string id, CancellationToken cancellationToken)
    {
        var result = await _userService.ActivateUserAsync(new ActivateUserCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/deactivate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Users)]
    [OpenApiOperation("Deactivate a user account.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeactivateUser(string id, CancellationToken cancellationToken)
    {
        var result = await _userService.DeactivateUserAsync(new DeactivateUserCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    // Staging a tenant migration moved to the provider page as a bulk action; see
    // OidcProvidersController. Cancelling a single user's pending migration stays here —
    // there is no bulk-cancel equivalent.

    [HttpDelete("{id}/stage-migration")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Users)]
    [OpenApiOperation("Cancel a pending tenant migration for a user.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> CancelTenantMigration(string id, CancellationToken cancellationToken)
    {
        var result = await _userService.CancelTenantMigration(id, cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/convert-to-local")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Users)]
    [OpenApiOperation("Convert an OIDC user to a local account with a password. The change is immediate and irreversible without admin intervention.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ConvertToLocalAccount(string id, ConvertToLocalAccountRequest request, CancellationToken cancellationToken)
    {
        var result = await _userService.ConvertToLocalAccount(
            new ConvertToLocalAccountCommand(id, request.NewPassword),
            cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/stage-provider-migration")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Users)]
    [OpenApiOperation("Stage a cross-provider identity migration for a user. The rebind completes on the user's next sign-in via the target provider.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> StageProviderMigration(string id, StageProviderMigrationRequest request, CancellationToken cancellationToken)
    {
        var result = await _userService.StageProviderMigration(
            new StageProviderMigrationCommand(id, request.TargetProviderId),
            cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/stage-provider-migration")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Users)]
    [OpenApiOperation("Cancel a pending cross-provider migration for a user.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> CancelProviderMigration(string id, CancellationToken cancellationToken)
    {
        var result = await _userService.CancelProviderMigration(id, cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id}/identity-history")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Users)]
    [OpenApiOperation("Get a user's identity history (active and inactive linked identities).", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<List<UserIdentityDto>> GetIdentityHistory(string id, CancellationToken cancellationToken)
    {
        return await _userService.GetIdentityHistory(id, cancellationToken);
    }

    private string GetOriginFromRequest() => $"{Request.Scheme}://{Request.Host.Value}{Request.PathBase.Value}";
}