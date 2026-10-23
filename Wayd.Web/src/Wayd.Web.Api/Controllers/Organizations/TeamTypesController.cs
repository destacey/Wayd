using Wayd.Organization.Application.TeamTypes.Dtos;
using Wayd.Organization.Application.TeamTypes.Queries;

namespace Wayd.Web.Api.Controllers.Organizations;

[Route("api/organization/team-types")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Organization, "wayd-teams")]
public class TeamTypesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public TeamTypesController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get a list of all team types.")]
    [EndpointDescription("The fixed kinds of team, in order: Team (people who work together on a prioritized backlog) and Team of Teams (a group of teams and other teams of teams). Each has a numeric `id`, `name` and `description`; a team's `type` names one of them.")]
    [McpTool("TeamTypes_GetList", "List team types")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TeamTypeDto>>> GetList(CancellationToken cancellationToken)
    {
        var categories = await _dispatcher.Send(new GetTeamTypesQuery(), cancellationToken);
        return Ok(categories.OrderBy(c => c.Order));
    }
}
