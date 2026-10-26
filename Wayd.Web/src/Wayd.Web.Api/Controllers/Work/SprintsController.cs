using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.FeatureManagement;
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
[McpTools(McpToolset.Work, "wayd-work")]
public class SprintsController(ILogger<SprintsController> logger, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<SprintsController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Iterations)]
    [EndpointSummary("Get a list of sprints.")]
    [EndpointDescription("Every sprint of every team, or of one team when `teamId` (the team's UUID) is given; pass it whenever the team is known, since the full list is long. Each sprint has a UUID `id` and an integer `key`, its planned `start` and `end`, its `state` now (Future, Active or Completed, worked out from its actual and planned dates), the `started` and `completed` moments the team recorded, and its `sprintType`.")]
    [McpTool("Sprints_GetSprints", "List sprints")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<SprintListDto>>> GetSprints([FromQuery] Guid? teamId, CancellationToken cancellationToken)
    {
        var sprints = await _dispatcher.Send(new GetSprintsQuery(teamId), cancellationToken);

        return Ok(sprints);
    }

    [HttpGet("types")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Iterations)]
    [EndpointSummary("Get a list of all sprint types.")]
    [EndpointDescription("The sprint types and what each means: Standard, comparable with the team's other sprints, and NonStandard, left out of the team's sprint metrics. Each `code` is the value `Sprints_SetSprintType` takes.")]
    [McpTool("Sprints_GetSprintTypes", "List sprint types")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SprintTypeDto>>> GetSprintTypes(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetSprintTypesQuery(), cancellationToken);
        return Ok(items.OrderBy(t => t.Order));
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Iterations)]
    [EndpointSummary("Get sprint details.")]
    [EndpointDescription("`idOrKey` takes the sprint's UUID `id` or integer `key`. Besides its dates, state, team and type, it says what the caller can do now: `canManageSprint` (the caller may start, complete or reopen it), `canStart`, `canComplete` and `canReopen` (the sprint's lifecycle allows it, whoever asks), `startWindow` and `completeWindow` (the moments a start or completion may be recorded at), and `openSprint`, another of the team's sprints still open that starting this one would complete. Read it before any sprint lifecycle call.")]
    [McpTool("Sprints_GetSprint", "Get sprint")]
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
    [EndpointSummary("Get activity history for the sprint.")]
    [EndpointDescription("`idOrKey` takes the sprint's UUID `id` or integer `key`. Newest first: syncs changing its details, dates or team, and the team starting, completing or reopening it, correcting its actual dates, setting its team days off or its type. Each entry has a `category`, an `actorKind` with the acting `employee` when there is one, a `timestamp`, a one-line `summary`, and a `payload`: the event's fields as a JSON string, carrying the value before and after a change. Paged: the response carries `totalCount` and `hasNextPage`.")]
    [McpTool("Sprints_GetActivities", "Get sprint activity history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = PageRequest.FirstPage, [FromQuery] int pageSize = PageSizes.Default, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(new GetSprintActivitiesQuery(new IdOrKey(idOrKey), new PageRequest(page, pageSize)), cancellationToken);

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    // get backlog
    [HttpGet("{idOrKey}/backlog")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Iterations)]
    [EndpointSummary("Get sprint backlog items.")]
    [EndpointDescription("`idOrKey` takes the sprint's UUID `id` or integer `key`. The work items in the sprint now, in backlog rank order. For what the sprint committed to and what became of it, use `Sprints_GetScope`.")]
    [McpTool("Sprints_GetBacklog", "Get sprint backlog")]
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
    [EndpointSummary("Get sprint work item metrics.")]
    [EndpointDescription("`idOrKey` takes the sprint's UUID `id` or integer `key`. Counts and estimates of the work in the sprint now — total, completed, in progress, not started and unestimated — with a cycle-time rollup. Estimates are in `sizingMethod`, the team's on the sprint's planned start. Not history: for the commitment and what became of it use `Sprints_GetScope`, and for the trend `Sprints_GetBurn`.")]
    [McpTool("Sprints_GetMetrics", "Get sprint metrics")]
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
    [EndpointSummary("Get sprint scope.")]
    [McpTool("Sprints_GetScope", "Get sprint scope")]
    [EndpointDescription("`idOrKey` takes the sprint's UUID `id` or integer `key`. What the sprint committed to and what became of it, worked out from work item history between its effective start and end: each requirement-tier item that was in the sprint, whether it was committed or added, and whether it was completed, completed as Removed, carried over or descoped. Not found when the sprint has no planned dates.")]
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
    [EndpointSummary("Get sprint burn-up and burn-down.")]
    [McpTool("Sprints_GetBurn", "Get sprint burn-up and burn-down")]
    [EndpointDescription("`idOrKey` takes the sprint's UUID `id` or integer `key`. The sprint's requirement-tier scope and completed work at its commitment point, at the end of each of its days in the team's zone, and at its effective end — or now, for a sprint that has not ended — as counts and estimates, worked out from work item history. Agrees with the sprint scope report at the commitment point and the end. Not found when the sprint has no planned dates.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SprintBurnDto>> GetSprintBurn(string idOrKey, CancellationToken cancellationToken)
    {
        var burn = await _dispatcher.Send(new GetSprintBurnQuery(idOrKey), cancellationToken);

        return burn is not null
            ? Ok(burn)
            : NotFound();
    }

    [HttpGet("{idOrKey}/capacity")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Iterations)]
    [EndpointSummary("Get a sprint's capacity beside its scope.")]
    [McpTool("Sprints_GetCapacity", "Get sprint capacity")]
    [EndpointDescription(
        "`idOrKey` takes the sprint's UUID `id` or integer `key`. The team's capacity over the sprint's planned dates (`start` to `end`) in person-days after holidays and time away: `allocatedPersonDays` in every role, `deliveryPersonDays` in roles that count toward delivery capacity, and `awayPersonDays` that time away took. Beside it, the sprint's `scope` in `sizingMethod` (by count under Count): the work in the sprint now before its commitment point (`scopeBasis` Planned), and the committed work after it (Committed). `expectedScope` is what the capacity is expected to deliver: the team's velocity per person-day of delivery capacity over the completed sprints its sprint metrics count now that ended no later than this sprint (`priorSprints`), in the same unit, times this sprint's. A sprint some way back therefore draws on fewer prior sprints than the team had at the time, or none. `isOverCapacity` is true when the scope is above it on a sprint that has not ended. `outcome` says whether the comparison was made, or why not: NoCapacity, NoPriorSprints, NoPriorCapacity. Not found when the sprint has no team or no planned dates. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SprintCapacityDto>> GetSprintCapacity(string idOrKey, CancellationToken cancellationToken)
    {
        var capacity = await _dispatcher.Send(new GetSprintCapacityQuery(idOrKey), cancellationToken);

        return capacity is not null
            ? Ok(capacity)
            : NotFound();
    }

    [HttpPost("{id}/start")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Iterations)]
    [EndpointSummary("Start a sprint.")]
    [EndpointDescription("Records that the team started the sprint, now or at an earlier startedAt inside its start window. `id` is the sprint's UUID; an integer key is not accepted. Requires membership of the sprint's team or its team of teams, and a caller linked to an employee. When another of the team's sprints is open, completeOpenSprintId must name it to confirm completing it at the same moment. `Sprints_GetSprint` reports `canStart`, the `startWindow` and the `openSprint`.")]
    [McpTool("Sprints_Start", "Start a sprint")]
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
    [EndpointSummary("Complete a sprint.")]
    [EndpointDescription("Records that the team completed the sprint, now or at an earlier completedAt inside its completion window. `id` is the sprint's UUID. Requires membership of the sprint's team or its team of teams, and a caller linked to an employee. `Sprints_GetSprint` reports `canComplete` and the `completeWindow`.")]
    [McpTool("Sprints_Complete", "Complete a sprint")]
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
    [EndpointSummary("Reopen a sprint.")]
    [EndpointDescription("Clears a completed sprint's completion while the team has not started a later sprint. `id` is the sprint's UUID. Requires membership of the sprint's team or its team of teams, and a caller linked to an employee. `Sprints_GetSprint` reports `canReopen`.")]
    [McpTool("Sprints_Reopen", "Reopen a sprint")]
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
    [EndpointSummary("Set a sprint's team days off.")]
    [EndpointDescription("Replaces the days within the sprint's planned dates that the whole team is off, such as an offsite, each with the `percentOff` of the working day (100, a full day, when left out). A day off takes its share of everyone's capacity that day, and the sprint's ideal burn-down drops that much less: flat on a full day off, half a day on a half day. `id` is the sprint's UUID. The list replaces the sprint's team days off: a day left out is removed and an empty list clears them, so read `teamDaysOff` from `Sprints_GetSprint` and send back the days that should stay. Requires membership of the sprint's team or its team of teams, and a caller linked to an employee.")]
    [McpTool("Sprints_SetTeamDaysOff", "Set sprint team days off")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> SetTeamDaysOff(Guid id, [FromBody] SetSprintTeamDaysOffRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new SetSprintTeamDaysOffCommand(id, request.ToTeamDaysOff()), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/type")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Iterations)]
    [EndpointSummary("Set or clear a sprint's type.")]
    [EndpointDescription("Sets the type the team gives the sprint, held whatever its planning interval mapping says; a null type clears it, so the sprint follows the mapped iteration's category again. A non-standard sprint keeps its own metrics but is left out of the team's sprint metrics. `id` is the sprint's UUID; `sprintType` is Standard, NonStandard or null. Requires membership of the sprint's team or its team of teams, and a caller linked to an employee.")]
    [McpTool("Sprints_SetSprintType", "Set or clear a sprint's type")]
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
    [EndpointSummary("Correct sprints' actual dates.")]
    [EndpointDescription("Replaces the actual start and completion of one or more of a team's sprints; an omitted value reverts to the sprint's default. Sprints corrected together are checked against each other's corrected dates, and the team's actual sprint periods may not overlap. Each entry takes a sprint's UUID `sprintId` and replaces both its `started` and `completed`, so send the current value of the one you are not changing (`started` and `completed` from `Sprints_GetSprint`), or it reverts to the planned date. Neither may be in the future. Requires membership of the sprints' team or its team of teams, and a caller linked to an employee.")]
    [McpTool("Sprints_CorrectActualDates", "Correct sprint actual dates")]
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
    [EndpointSummary("Get planning intervals that this sprint is mapped to.")]
    [EndpointDescription("`key` takes the sprint's integer `key` only, not its UUID. Returns each planning interval the sprint is mapped to through one of its iterations, by name; an unmapped sprint returns an empty list.")]
    [McpTool("Sprints_GetPlanningIntervals", "Get sprint planning intervals")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<NavigationDto>>> GetPlanningIntervals(int key, CancellationToken cancellationToken)
    {
        var planningIntervals = await _dispatcher.Send(new GetSprintPlanningIntervalsQuery(key), cancellationToken);
        return Ok(planningIntervals);
    }
}
