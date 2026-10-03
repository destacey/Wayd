using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Planning.Application.PlanningSprints.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Work.Sprints;
using Wayd.Work.Application.Iterations.Commands;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.Iterations.Queries;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Queries;

namespace Wayd.Web.Api.Controllers.Work;

[Route("api/work/sprints")]
[ApiVersionNeutral]
[ApiController]
public class SprintsController(ILogger<SprintsController> logger, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<SprintsController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Iterations)]
    [OpenApiOperation("Get a list of sprints.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<SprintListDto>>> GetSprints([FromQuery] Guid? teamId, CancellationToken cancellationToken)
    {
        var sprints = await _dispatcher.Send(new GetSprintsQuery(teamId), cancellationToken);

        return Ok(sprints);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Iterations)]
    [OpenApiOperation("Get sprint details.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SprintDetailsDto>> GetSprint(string idOrKey, CancellationToken cancellationToken)
    {
        var sprint = await _dispatcher.Send(new GetSprintQuery(idOrKey), cancellationToken);

        return sprint is not null
            ? Ok(sprint)
            : NotFound();
    }

    [HttpGet("{idOrKey}/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Iterations)]
    [OpenApiOperation("Get activity history for the sprint.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(new GetSprintActivitiesQuery(new IdOrKey(idOrKey), page, pageSize), cancellationToken);

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    // get backlog
    [HttpGet("{idOrKey}/backlog")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Iterations)]
    [OpenApiOperation("Get sprint backlog items.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<SprintBacklogItemDto>>> GetSprintBacklog(string idOrKey, CancellationToken cancellationToken)
    {
        var backlogItems = await _dispatcher.Send(new GetSprintBacklogQuery(idOrKey), cancellationToken);

        return backlogItems is not null
            ? Ok(backlogItems)
            : NotFound();
    }

    [HttpGet("{idOrKey}/metrics")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Iterations)]
    [OpenApiOperation("Get sprint work item metrics.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SprintWorkItemMetricsDto>> GetSprintMetrics(string idOrKey, CancellationToken cancellationToken)
    {
        var metrics = await _dispatcher.Send(new GetSprintWorkItemMetricsQuery(idOrKey), cancellationToken);

        return metrics is not null
            ? Ok(metrics)
            : NotFound();
    }

    [HttpPost("{id}/start")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Iterations)]
    [OpenApiOperation("Start a sprint.", "Records that the team started the sprint, now or at an earlier startedAt inside its start window. Requires membership of the sprint's team or its team of teams. When another of the team's sprints is open, completeOpenSprintId must name it to confirm completing it at the same moment.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Start(Guid id, [FromBody] StartSprintRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new StartSprintCommand(id, request.CompleteOpenSprintId, request.StartedAt), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/complete")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Iterations)]
    [OpenApiOperation("Complete a sprint.", "Records that the team completed the sprint, now or at an earlier completedAt inside its completion window. Requires membership of the sprint's team or its team of teams.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Complete(Guid id, [FromBody] CompleteSprintRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new CompleteSprintCommand(id, request.CompletedAt), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/reopen")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Iterations)]
    [OpenApiOperation("Reopen a sprint.", "Clears a completed sprint's completion while the team has not started a later sprint. Requires membership of the sprint's team or its team of teams.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Reopen(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ReopenSprintCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{key:int}/planning-intervals")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Iterations)]
    [OpenApiOperation("Get planning intervals that this sprint is mapped to.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<NavigationDto>>> GetPlanningIntervals(int key, CancellationToken cancellationToken)
    {
        var planningIntervals = await _dispatcher.Send(new GetSprintPlanningIntervalsQuery(key), cancellationToken);
        return Ok(planningIntervals);
    }
}
