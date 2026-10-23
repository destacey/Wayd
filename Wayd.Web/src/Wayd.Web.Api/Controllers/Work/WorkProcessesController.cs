using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Work.Application.WorkProcesses.Commands;
using Wayd.Work.Application.WorkProcesses.Dtos;
using Wayd.Work.Application.WorkProcesses.Queries;

namespace Wayd.Web.Api.Controllers.Work;

[Route("api/work/work-processes")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Work, "wayd-work")]
public class WorkProcessesController(ILogger<WorkProcessesController> logger, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<WorkProcessesController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkProcesses)]
    [EndpointSummary("Get a list of work processes.")]
    [EndpointDescription("A work process defines the work types a workspace's work items can have and the workflow each follows; work processes sync from an external system such as Azure DevOps (`ownership` Managed). Each has a UUID `id` and an integer `key`. Active work processes only unless `includeInactive` is true.")]
    [McpTool("WorkProcesses_GetWorkProcesses", "List work processes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<WorkProcessListDto>>> GetList(CancellationToken cancellationToken, bool includeInactive = false)
    {
        var workProcesses = await _dispatcher.Send(new GetWorkProcessesQuery(includeInactive), cancellationToken);
        return Ok(workProcesses.OrderBy(s => s.Name));
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkProcesses)]
    [EndpointSummary("Get work process details.")]
    [EndpointDescription("`idOrKey` takes the work process's UUID `id` or integer `key`. For its work types and their workflows, use `WorkProcesses_GetSchemes`.")]
    [McpTool("WorkProcesses_GetWorkProcess", "Get work process")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkProcessDto>> Get(string idOrKey, CancellationToken cancellationToken)
    {
        GetWorkProcessQuery query;
        if (Guid.TryParse(idOrKey, out Guid guidId))
        {
            query = new GetWorkProcessQuery(guidId);
        }
        else if (int.TryParse(idOrKey, out int intId))
        {
            query = new GetWorkProcessQuery(intId);
        }
        else
        {
            return BadRequest(ProblemDetailsExtensions.ForUnknownIdOrKeyType(HttpContext));
        }

        var result = await _dispatcher.Send(query, cancellationToken);

        return result.IsFailure
            ? BadRequest(result.ToBadRequestObject(HttpContext))
            : result.Value is not null
                ? Ok(result.Value)
                : NotFound();
    }

    [HttpGet("{idOrKey}/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkProcesses)]
    [EndpointSummary("Get activity history for the work process.")]
    [EndpointDescription("`idOrKey` takes the work process's UUID `id` or integer `key`. Newest first; for a work process this is its integration state changing, as it is activated, deactivated or synced. Each entry has a `category`, an `actorKind` with the acting `employee` when there is one, a `timestamp`, a one-line `summary`, and a `payload`: the event's fields as a JSON string. Paged: the response carries `totalCount` and `hasNextPage`.")]
    [McpTool("WorkProcesses_GetActivities", "Get work process activity history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        // Checked here so a malformed idOrKey answers 400, as Get above does; IdOrKey's string
        // constructor would throw NotFoundException, a 404.
        if (!Guid.TryParse(idOrKey, out _) && !int.TryParse(idOrKey, out _))
        {
            return BadRequest(ProblemDetailsExtensions.ForUnknownIdOrKeyType(HttpContext));
        }

        var result = await _dispatcher.Send(new GetWorkProcessActivitiesQuery(new IdOrKey(idOrKey), page, pageSize), cancellationToken);

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpPost("{id}/activate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.WorkProcesses)]
    [EndpointSummary("Activate a work process.")]
    [EndpointDescription("`id` is the work process's UUID. Only an active work process can be assigned to a workspace. Refused when it is already active.")]
    [McpTool("WorkProcesses_Activate", "Activate a work process")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ActivateWorkProcessCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/deactivate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.WorkProcesses)]
    [EndpointSummary("Deactivate a work process.")]
    [EndpointDescription("`id` is the work process's UUID. Refused while an active workspace uses it, and when it is already inactive.")]
    [McpTool("WorkProcesses_Deactivate", "Deactivate a work process")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeactivateWorkProcessCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id}/schemes")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkProcesses)]
    [EndpointSummary("Get work process schemes.")]
    [EndpointDescription("`id` is the work process's UUID only. Returns one scheme per work type in the process: the `workType` (with its level), the `workflow` it follows, and whether the work type is active in this process. Returns 404 when no work process has the id.")]
    [McpTool("WorkProcesses_GetSchemes", "Get work process schemes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<WorkProcessSchemeDto>>> GetSchemes(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetWorkProcessSchemesQuery(id), cancellationToken);

        return result is not null
                ? Ok(result)
                : NotFound();
    }
}
