using Wayd.Work.Application.WorkTypeTiers.Dtos;
using Wayd.Work.Application.WorkTypeTiers.Queries;

namespace Wayd.Web.Api.Controllers.Work;

[Route("api/work/work-type-tiers")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Work, "wayd-work")]
public class WorkTypeTiersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public WorkTypeTiersController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkTypeTiers)]
    [EndpointSummary("Get a list of all work type tiers.")]
    [EndpointDescription("The fixed tiers that group work type levels by purpose, with what each means: Portfolio (containers such as Epics and Features, the only tier that can be parents and the only one with several levels), Requirement (a team's backlog work, such as stories and bugs), Task (work owned by a parent item) and Other (left out of backlog and sprint views).")]
    [McpTool("WorkTypeTiers_GetWorkTypeTiers", "List work type tiers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<WorkTypeTierDto>>> GetList(CancellationToken cancellationToken)
    {
        var tiers = await _dispatcher.Send(new GetWorkTypeTiersQuery(), cancellationToken);
        return Ok(tiers.OrderBy(c => c.Order));
    }
}
