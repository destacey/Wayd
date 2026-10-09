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

    [HttpGet("types")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Iterations)]
    [OpenApiOperation("Get a list of all sprint types.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SprintTypeDto>>> GetSprintTypes(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetSprintTypesQuery(), cancellationToken);
        return Ok(items.OrderBy(t => t.Order));
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

    [HttpGet("{idOrKey}/scope")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Iterations)]
    [OpenApiOperation("Get sprint scope.", "What the sprint committed to and what became of it, worked out from work item history between its effective start and end: each requirement-tier item that was in the sprint, whether it was committed or added, and whether it was completed, completed as Removed, carried over or descoped. Not found when the sprint has no planned dates.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SprintScopeDto>> GetSprintScope(string idOrKey, CancellationToken cancellationToken)
    {
        var scope = await _dispatcher.Send(new GetSprintScopeQuery(idOrKey), cancellationToken);

        return scope is not null
            ? Ok(scope)
            : NotFound();
    }

    [HttpGet("{idOrKey}/burn")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Iterations)]
    [OpenApiOperation("Get sprint burn-up and burn-down.", "The sprint's requirement-tier scope and completed work at its commitment point, at the end of each of its days in the team's zone, and at its effective end — or now, for a sprint that has not ended — as counts and estimates, worked out from work item history. Agrees with the sprint scope report at the commitment point and the end. Not found when the sprint has no planned dates.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SprintBurnDto>> GetSprintBurn(string idOrKey, CancellationToken cancellationToken)
    {
        var burn = await _dispatcher.Send(new GetSprintBurnQuery(idOrKey), cancellationToken);

        return burn is not null
            ? Ok(burn)
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

    [HttpPut("{id}/team-days-off")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Iterations)]
    [OpenApiOperation("Set a sprint's team days off.", "Replaces the days within the sprint's planned dates that the whole team is off, such as an offsite; the sprint's ideal burn-down stays flat on them. Requires membership of the sprint's team or its team of teams.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetTeamDaysOff(Guid id, [FromBody] SetSprintTeamDaysOffRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new SetSprintTeamDaysOffCommand(id, request.TeamDaysOff), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/type")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Iterations)]
    [OpenApiOperation("Set or clear a sprint's type.", "Sets the type the team gives the sprint, held whatever its planning interval mapping says; a null type clears it, so the sprint follows the mapped iteration's category again. A non-standard sprint keeps its own metrics but is left out of rollups across sprints. Requires membership of the sprint's team or its team of teams.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetSprintType(Guid id, [FromBody] SetSprintTypeRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new SetSprintTypeCommand(id, request.SprintType), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("actual-dates")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Iterations)]
    [OpenApiOperation("Correct sprints' actual dates.", "Replaces the actual start and completion of one or more of a team's sprints; an omitted value reverts to the sprint's default. Sprints corrected together are checked against each other's corrected dates, and the team's actual sprint periods may not overlap. Requires membership of the sprints' team or its team of teams.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CorrectActualDates([FromBody] CorrectSprintActualDatesRequest request, CancellationToken cancellationToken)
    {
        var command = new CorrectSprintActualDatesCommand(
            [.. request.Sprints.Select(s => new SprintActualDatesCorrection(s.SprintId, s.Started, s.Completed))]);
        var result = await _dispatcher.Send(command, cancellationToken);

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
