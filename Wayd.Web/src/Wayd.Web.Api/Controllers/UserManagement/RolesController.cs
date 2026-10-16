using Wayd.Common.Application.Exceptions;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.UserManagement.Roles;

namespace Wayd.Web.Api.Controllers.UserManagement;

[Route("api/user-management/roles")]
[ApiVersionNeutral]
[ApiController]
public class RolesController(IRoleService roleService, IUserService userService, ILogger<RolesController> logger) : ControllerBase
{
    private readonly IRoleService _roleService = roleService;
    private readonly IUserService _userService = userService;
    private readonly ILogger<RolesController> _logger = logger;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Roles)]
    [EndpointSummary("Get a list of all roles.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RoleListDto>>> GetList(CancellationToken cancellationToken)
    {
        var roles = await _roleService.GetList(cancellationToken);

        return Ok(roles);
    }

    [HttpGet("{id}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Roles)]
    [EndpointSummary("Get role details.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleDto>> GetById(string id, CancellationToken cancellationToken)
    {
        var role = await _roleService.GetById(id, cancellationToken);

        return role is not null
            ? Ok(role)
            : NotFound();
    }

    [HttpGet("{id}/permissions")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.RoleClaims)]
    [EndpointSummary("Get role details with its permissions.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleDto>> GetByIdWithPermissions(string id, CancellationToken cancellationToken)
    {
        var role = await _roleService.GetByIdWithPermissions(id, cancellationToken);

        return role is not null
            ? Ok(role)
            : NotFound();
    }

    [HttpPut("{id}/permissions")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.RoleClaims)]
    [EndpointSummary("Update a role's permissions.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdatePermissions(string id, UpdateRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        if (id != request.RoleId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.RoleId), HttpContext));

        var result = await _roleService.UpdatePermissions(request.ToUpdateRolePermissionsCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Roles)]
    [EndpointSummary("Create or update a role.")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201String))]
    public async Task<ActionResult<string>> CreateOrUpdate(CreateOrUpdateRoleRequest request)
    {
        var id = await _roleService.CreateOrUpdate(request.ToCreateOrUpdateRoleCommand());

        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpGet("{id}/users")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Roles)]
    [EndpointSummary("Get list of all users with this role.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<List<UserDetailsDto>> GetUsers(string id, CancellationToken cancellationToken)
    {
        return await _userService.GetUsersWithRole(id, cancellationToken);
    }

    [HttpGet("{id}/users-count")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Roles)]
    [EndpointSummary("Get a count of all users with this role.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<int> GetUsersCount(string id, CancellationToken cancellationToken)
    {
        return await _userService.GetUsersWithRoleCount(id, cancellationToken);
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.Roles)]
    [EndpointSummary("Delete a role.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(string id)
    {
        try
        {
            await _roleService.Delete(id);
        }
        catch (ConflictException ex)
        {
            _logger.LogError(ex, "Error deleting role with id {id}", id);
            return Conflict(ProblemDetailsExtensions.ForConflict(ex.Message, HttpContext));
        }

        return NoContent();
    }
}