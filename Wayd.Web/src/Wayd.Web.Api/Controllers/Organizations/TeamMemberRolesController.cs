using Wayd.Organization.Application.TeamMemberRoles.Commands;
using Wayd.Organization.Application.TeamMemberRoles.Dtos;
using Wayd.Organization.Application.TeamMemberRoles.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Organizations.TeamMemberRoles;

namespace Wayd.Web.Api.Controllers.Organizations;

[Route("api/organization/team-member-roles")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Organization, "wayd-teams")]
public class TeamMemberRolesController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.TeamMemberRoles)]
    [EndpointSummary("Get a list of team member roles.")]
    [EndpointDescription("The roles a person can hold on a team or team of teams, such as Developer or Scrum Master, by name. Active roles only unless `includeInactive` is true. Each has its UUID `id` (what the team member tools take as `roleIds`), `key`, name, description, `isActive`, and `countsTowardDeliveryCapacity`: whether time allocated in the role adds to a team's delivery capacity.")]
    [McpTool("TeamMemberRoles_GetList", "List team member roles")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TeamMemberRoleDto>>> GetList(CancellationToken cancellationToken, bool includeInactive = false)
    {
        var roles = await dispatcher.Send(new GetTeamMemberRolesQuery(includeInactive), cancellationToken);
        return Ok(roles);
    }

    [HttpGet("{id:guid}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.TeamMemberRoles)]
    [EndpointSummary("Get a team member role by id.")]
    [EndpointDescription("Takes the role's UUID `id`.")]
    [McpTool("TeamMemberRoles_GetRole", "Get team member role")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamMemberRoleDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var role = await dispatcher.Send(new GetTeamMemberRoleQuery(id), cancellationToken);
        return role is not null ? Ok(role) : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.TeamMemberRoles)]
    [EndpointSummary("Create a team member role.")]
    [EndpointDescription("`name` is unique across roles. `countsTowardDeliveryCapacity` defaults to true; set it false for a role, such as a manager's, whose time on a team does not burn its planned work. The role starts active. Returns the new role's UUID.")]
    [McpTool("TeamMemberRoles_Create", "Create team member role", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult> Create([FromBody] CreateTeamMemberRoleRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new CreateTeamMemberRoleCommand(request.Name, request.Description, request.CountsTowardDeliveryCapacity ?? true), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id:guid}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.TeamMemberRoles)]
    [EndpointSummary("Update a team member role.")]
    [EndpointDescription("Takes the role's UUID `id`, which must match the body's `id`. **`name` and `description` are overwritten: an omitted description is cleared**; an omitted `countsTowardDeliveryCapacity` is left as it is. Changing `countsTowardDeliveryCapacity` changes every team's delivery capacity for time allocated in the role, past and future.")]
    [McpTool("TeamMemberRoles_Update", "Update team member role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateTeamMemberRoleRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await dispatcher.Send(new UpdateTeamMemberRoleCommand(request.Id, request.Name, request.Description, request.CountsTowardDeliveryCapacity), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id:guid}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.TeamMemberRoles)]
    [EndpointSummary("Delete a team member role.")]
    [EndpointDescription("Takes the role's UUID `id`. Refused while any team member holds the role or any allocation uses it; deactivate it instead with TeamMemberRoles_Deactivate.")]
    [McpTool("TeamMemberRoles_Delete", "Delete team member role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new DeleteTeamMemberRoleCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id:guid}/activate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.TeamMemberRoles)]
    [EndpointSummary("Activate a team member role.")]
    [EndpointDescription("Takes the role's UUID `id`. Makes a deactivated role listed and offered again.")]
    [McpTool("TeamMemberRoles_Activate", "Activate team member role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ActivateTeamMemberRoleCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id:guid}/deactivate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.TeamMemberRoles)]
    [EndpointSummary("Deactivate a team member role.")]
    [EndpointDescription("Takes the role's UUID `id`. The role drops out of TeamMemberRoles_GetList (unless `includeInactive`) and of allocation proposals; members who hold it keep it.")]
    [McpTool("TeamMemberRoles_Deactivate", "Deactivate team member role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new DeactivateTeamMemberRoleCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
