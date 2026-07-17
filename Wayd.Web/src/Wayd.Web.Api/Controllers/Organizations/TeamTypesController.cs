using Wayd.Organization.Application.TeamTypes.Dtos;
using Wayd.Organization.Application.TeamTypes.Queries;

namespace Wayd.Web.Api.Controllers.Work;

[Route("api/organization/team-types")]
[ApiVersionNeutral]
[ApiController]
public class TeamTypesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public TeamTypesController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkTypeTiers)]
    [OpenApiOperation("Get a list of all team types.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TeamTypeDto>>> GetList(CancellationToken cancellationToken)
    {
        var categories = await _dispatcher.Send(new GetTeamTypesQuery(), cancellationToken);
        return Ok(categories.OrderBy(c => c.Order));
    }
}
