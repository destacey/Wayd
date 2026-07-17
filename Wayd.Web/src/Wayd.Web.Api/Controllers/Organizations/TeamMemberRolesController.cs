using Wayd.Organization.Application.TeamMemberRoles.Commands;
using Wayd.Organization.Application.TeamMemberRoles.Dtos;
using Wayd.Organization.Application.TeamMemberRoles.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Organizations.TeamMemberRoles;

namespace Wayd.Web.Api.Controllers.Organizations;

[Route("api/organization/team-member-roles")]
[ApiVersionNeutral]
[ApiController]
public class TeamMemberRolesController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.TeamMemberRoles)]
    [OpenApiOperation("Get a list of team member roles.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TeamMemberRoleDto>>> GetList(CancellationToken cancellationToken, bool includeInactive = false)
    {
        var roles = await dispatcher.Send(new GetTeamMemberRolesQuery(includeInactive), cancellationToken);
        return Ok(roles);
    }

    [HttpGet("{id:guid}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.TeamMemberRoles)]
    [OpenApiOperation("Get a team member role by id.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamMemberRoleDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var role = await dispatcher.Send(new GetTeamMemberRoleQuery(id), cancellationToken);
        return role is not null ? Ok(role) : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.TeamMemberRoles)]
    [OpenApiOperation("Create a team member role.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult> Create([FromBody] CreateTeamMemberRoleRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new CreateTeamMemberRoleCommand(request.Name, request.Description), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id:guid}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.TeamMemberRoles)]
    [OpenApiOperation("Update a team member role.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateTeamMemberRoleRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await dispatcher.Send(new UpdateTeamMemberRoleCommand(request.Id, request.Name, request.Description), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id:guid}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.TeamMemberRoles)]
    [OpenApiOperation("Delete a team member role.", "")]
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
    [OpenApiOperation("Activate a team member role.", "")]
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
    [OpenApiOperation("Deactivate a team member role.", "")]
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
