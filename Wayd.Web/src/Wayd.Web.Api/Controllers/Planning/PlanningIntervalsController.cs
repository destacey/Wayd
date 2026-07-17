using CSharpFunctionalExtensions;
using CsvHelper;
using Mapster;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Extensions;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.TeamsOfTeams.Queries;
using Wayd.Planning.Application.PlanningIntervals.Commands;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Application.PlanningIntervals.HealthChecks.Commands;
using Wayd.Planning.Application.PlanningIntervals.HealthChecks.Queries;
using Wayd.Planning.Application.PlanningIntervals.Queries;
using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Planning.Application.Risks.Queries;
using Wayd.Web.Api.Dtos.Planning;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Planning.HealthChecks;
using Wayd.Web.Api.Models.Planning.PlanningIntervals;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Queries;

namespace Wayd.Web.Api.Controllers.Planning;

[Route("api/planning/planning-intervals")]
[ApiVersionNeutral]
[ApiController]
public class PlanningIntervalsController : ControllerBase
{
    private readonly ILogger<PlanningIntervalsController> _logger;
    private readonly IDispatcher _dispatcher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICsvService _csvService;

    public PlanningIntervalsController(ILogger<PlanningIntervalsController> logger, IDispatcher dispatcher, IDateTimeProvider dateTimeProvider, ICsvService csvService)
    {
        _logger = logger;
        _dispatcher = dispatcher;
        _dateTimeProvider = dateTimeProvider;
        _csvService = csvService;
    }

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervals)]
    [OpenApiOperation("Get a list of planning intervals.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PlanningIntervalListDto>>> GetList(CancellationToken cancellationToken)
    {
        var planningIntervals = await _dispatcher.Send(new GetPlanningIntervalsQuery(), cancellationToken);
        return Ok(planningIntervals);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervals)]
    [OpenApiOperation("Get planning interval details.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanningIntervalDetailsDto>> GetPlanningInterval(string idOrKey, CancellationToken cancellationToken)
    {
        var planningInterval = await _dispatcher.Send(new GetPlanningIntervalQuery(idOrKey), cancellationToken);

        return planningInterval is not null
            ? Ok(planningInterval)
            : NotFound();
    }

    [HttpGet("{idOrKey}/calendar")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervals)]
    [OpenApiOperation("Get the PI calendar.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlanningIntervalCalendarDto>> GetCalendar(string idOrKey, CancellationToken cancellationToken)
    {
        var calendar = await _dispatcher.Send(new GetPlanningIntervalCalendarQuery(idOrKey), cancellationToken);

        return calendar is not null
            ? Ok(calendar)
            : NotFound();
    }

    [HttpGet("{idOrKey}/predictability")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervals)]
    [OpenApiOperation("Get the PI predictability for all teams.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PlanningIntervalPredictabilityDto>> GetPredictability(string idOrKey, CancellationToken cancellationToken)
    {
        var predictability = await _dispatcher.Send(new GetPlanningIntervalPredictabilityQuery(idOrKey), cancellationToken);

        return predictability is not null
            ? Ok(predictability)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.PlanningIntervals)]
    [OpenApiOperation("Create a planning interval.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult> Create([FromBody] CreatePlanningIntervalRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreatePlanningIntervalCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetPlanningInterval), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.PlanningIntervals)]
    [OpenApiOperation("Update a planning interval.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdatePlanningIntervalRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdatePlanningIntervalCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{idOrKey}/teams")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervals)]
    [OpenApiOperation("Get a list of planning interval teams.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PlanningIntervalTeamResponse>>> GetTeams(string idOrKey, CancellationToken cancellationToken)
    {
        List<PlanningIntervalTeamResponse> piTeams = [];
        var teamIds = await _dispatcher.Send(new GetPlanningIntervalTeamsQuery(idOrKey), cancellationToken);

        if (teamIds.Any())
        {
            var teams = await _dispatcher.Send(new GetTeamsQuery(true, teamIds), cancellationToken);
            var teamOfTeams = await _dispatcher.Send(new GetTeamOfTeamsListQuery(true, teamIds), cancellationToken);

            piTeams.AddRange(teams.Adapt<List<PlanningIntervalTeamResponse>>());
            piTeams.AddRange(teamOfTeams.Adapt<List<PlanningIntervalTeamResponse>>());
        }

        return Ok(piTeams);
    }

    [HttpGet("{idOrKey}/teams/{teamId}/predictability")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervals)]
    [OpenApiOperation("Get the PI predictability for a team.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<double?>> GetTeamPredictability(string idOrKey, Guid teamId, CancellationToken cancellationToken)
    {
        var predictability = await _dispatcher.Send(new GetTeamPlanningIntervalPredictabilityQuery(idOrKey, teamId), cancellationToken);

        return Ok(predictability);
    }

    [HttpPost("{id}/dates")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.PlanningIntervals)]
    [OpenApiOperation("Manage planning interval dates and iterations.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ManageDates(Guid id, [FromBody] ManagePlanningIntervalDatesRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToManagePlanningIntervalDatesCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/teams")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.PlanningIntervals)]
    [OpenApiOperation("Manage planning interval teams.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ManageTeams(Guid id, [FromBody] ManagePlanningIntervalTeamsRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToManagePlanningIntervalTeamsCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #region Iterations

    [HttpGet("{idOrKey}/iterations")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervals)]
    [OpenApiOperation("Get a list of planning interval iterations.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PlanningIntervalIterationListDto>>> GetIterations(string idOrKey, CancellationToken cancellationToken)
    {
        var iterations = await _dispatcher.Send(new GetPlanningIntervalIterationsQuery(idOrKey), cancellationToken);

        return iterations is not null
            ? Ok(iterations)
            : NotFound();
    }

    [HttpGet("{idOrKey}/iterations/{iterationIdOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervals)]
    [OpenApiOperation("Get a specific planning interval iteration.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanningIntervalIterationDetailsDto?>> GetIteration(string idOrKey, string iterationIdOrKey, CancellationToken cancellationToken)
    {
        var iteration = await _dispatcher.Send(new GetPlanningIntervalIterationQuery(idOrKey, iterationIdOrKey), cancellationToken);

        return iteration is not null
            ? Ok(iteration)
            : NotFound();
    }

    [HttpGet("iteration-categories")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervals)]
    [OpenApiOperation("Get a list of iteration categories.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<PlanningIntervalIterationCategoryDto>>> GetIterationCategories(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetPlanningIntervalIterationCategoriesQuery(), cancellationToken);
        return Ok(items.OrderBy(c => c.Order));
    }

    [HttpGet("{idOrKey}/iterations/sprints")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervals)]
    [OpenApiOperation("Get iteration sprint mappings for a Planning Interval.", "Retrieves all sprint-to-iteration mappings, with optional filtering by iteration.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PlanningIntervalIterationSprintsDto>>> GetIterationSprints(string idOrKey, [FromQuery] Guid? iterationId, CancellationToken cancellationToken)
    {
        var iterations = await _dispatcher.Send(new GetPlanningIntervalIterationSprintsQuery(idOrKey, iterationId), cancellationToken);

        return iterations is not null
            ? Ok(iterations)
            : NotFound();
    }

    [HttpPost("{id}/teams/{teamId}/sprints")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.PlanningIntervals)]
    [OpenApiOperation("Map team sprints to Planning Interval iterations.", "This is a sync/replace operation that sets the complete desired state for the team's sprint mappings.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> MapTeamSprints(Guid id, Guid teamId, [FromBody] MapPlanningIntervalSprintsRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.Id), HttpContext));

        if (teamId != request.TeamId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(teamId), nameof(request.TeamId), HttpContext));

        var result = await _dispatcher.Send(request.ToMapPlanningIntervalSprintsCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{idOrKey}/metrics")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervals)]
    [OpenApiOperation("Get PI-level metrics aggregated across all mapped sprints in every iteration.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanningIntervalMetricsResponse>> GetMetrics(string idOrKey, CancellationToken cancellationToken)
    {
        // Pull the PI itself for identity fields.
        var planningInterval = await _dispatcher.Send(
            new GetPlanningIntervalQuery(idOrKey),
            cancellationToken);

        if (planningInterval is null)
            return NotFound();

        // Pull sprint mappings for every iteration in the PI in one shot.
        var iterationSprints = await _dispatcher.Send(
            new GetPlanningIntervalIterationSprintsQuery(idOrKey, null),
            cancellationToken);

        if (iterationSprints is null)
            return NotFound();

        // Predictability is computed by the existing query and includes a
        // per-team breakdown — we surface those values here so the client only
        // needs one round-trip for the at-a-glance team cards.
        var predictability = await _dispatcher.Send(
            new GetPlanningIntervalPredictabilityQuery(new IdOrKey(idOrKey)),
            cancellationToken);

        // Flatten across iterations and de-dupe sprint ids — the same sprint
        // could in theory be mapped to multiple iterations and we don't want
        // to double-count its metrics.
        var sprintToTeam = iterationSprints
            .SelectMany(i => i.Sprints)
            .GroupBy(s => s.Id)
            .ToDictionary(
                g => g.Key,
                g => g.First().Team);
        var sprintIds = sprintToTeam.Keys.ToList();

        if (sprintIds.Count == 0)
        {
            return Ok(new PlanningIntervalMetricsResponse
            {
                PlanningIntervalId = planningInterval.Id,
                PlanningIntervalKey = planningInterval.Key,
                PlanningIntervalName = planningInterval.Name,
                TeamCount = 0,
                SprintCount = 0,
                CycleTime = CycleTimeSummary.Empty,
                TeamMetrics = [],
            });
        }

        var sprintMetrics = await _dispatcher.Send(
            new GetSprintsWorkItemMetricsQuery(sprintIds),
            cancellationToken);

        // Pull team codes for the participating teams so the response can carry
        // the short code the plan-review tabs route on. The PlanningTeamNavigationDto
        // we already have via sprintToTeam doesn't expose Code.
        var participatingTeamIds = sprintToTeam.Values.Select(t => t.Id).Distinct().ToList();
        var teamList = await _dispatcher.Send(
            new GetTeamsQuery(true, participatingTeamIds),
            cancellationToken);
        var codeByTeamId = teamList.ToDictionary(t => t.Id, t => t.Code);

        // Per-team rollup. Cycle time aggregates by summing each sprint's
        // count + total — composing CycleTimeSummary objects this way yields
        // a true weighted average (Σtotal / Σcount) rather than an
        // average-of-averages. Predictability comes from the existing
        // predictability query.
        var teamPredictabilityByTeamId = predictability?.TeamPredictabilities
            .ToDictionary(t => t.Team.Id)
            ?? [];

        var teamMetrics = sprintMetrics
            .Where(m => sprintToTeam.ContainsKey(m.SprintId))
            .GroupBy(m => sprintToTeam[m.SprintId].Id)
            .Select(g =>
            {
                var team = sprintToTeam[g.First().SprintId];
                teamPredictabilityByTeamId.TryGetValue(team.Id, out var teamPredictability);
                codeByTeamId.TryGetValue(team.Id, out var teamCode);
                var resolvedTeamCode = !string.IsNullOrWhiteSpace(teamCode) ? teamCode : team.Code;

                if (string.IsNullOrWhiteSpace(resolvedTeamCode))
                    throw new InvalidOperationException($"Unable to resolve a non-empty team code for team '{team.Name}' ({team.Id}).");

                return new PlanningIntervalTeamMetrics
                {
                    Team = new Common.Application.Dtos.NavigationDto { Id = team.Id, Key = team.Key, Name = team.Name },
                    TeamCode = resolvedTeamCode,
                    Predictability = teamPredictability?.Predictability,
                    RegularObjectivesCount = teamPredictability?.RegularObjectivesCount ?? 0,
                    StretchObjectivesCount = teamPredictability?.StretchObjectivesCount ?? 0,
                    CompletedObjectivesCount = teamPredictability?.CompletedObjectivesCount ?? 0,
                    CycleTime = CycleTimeSummary.Combine(g.Select(m => m.CycleTime)),
                    SprintCount = g.Count(),
                };
            })
            .OrderBy(t => t.Team.Name)
            .ToList();

        var teamCount = sprintToTeam.Values.Select(t => t.Id).Distinct().Count();

        return Ok(new PlanningIntervalMetricsResponse
        {
            PlanningIntervalId = planningInterval.Id,
            PlanningIntervalKey = planningInterval.Key,
            PlanningIntervalName = planningInterval.Name,
            TeamCount = teamCount,
            SprintCount = sprintIds.Count,
            CycleTime = CycleTimeSummary.Combine(sprintMetrics.Select(m => m.CycleTime)),
            TeamMetrics = teamMetrics,
        });
    }

    [HttpGet("{idOrKey}/iterations/{iterationIdOrKey}/metrics")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervals)]
    [OpenApiOperation("Get metrics for a PI iteration aggregated across all mapped sprints.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanningIntervalIterationMetricsResponse>> GetIterationMetrics(string idOrKey, string iterationIdOrKey, CancellationToken cancellationToken)
    {
        // Get the iteration with its mapped sprints from Planning context
        var iterationSprints = await _dispatcher.Send(
            new GetPlanningIntervalIterationSprintsQuery(idOrKey, null),
            cancellationToken);

        if (iterationSprints is null)
            return NotFound();

        // Find the specific iteration
        var iterationKey = new IdOrKey(iterationIdOrKey);
        var iteration = iterationSprints.FirstOrDefault(i =>
            iterationKey.IsId ? i.Id == iterationKey.AsId : i.Key == iterationKey.AsKey);

        if (iteration is null)
            return NotFound();

        // Get work item metrics from Work context
        var sprintIds = iteration.Sprints.Select(s => s.Id).ToList();
        var sprintMetrics = await _dispatcher.Send(
            new GetSprintsWorkItemMetricsQuery(sprintIds),
            cancellationToken);

        // Build per-sprint metrics summaries
        var sprintMetricsSummaries = iteration.Sprints.Select(sprint =>
        {
            var metrics = sprintMetrics.FirstOrDefault(m => m.SprintId == sprint.Id);
            return new SprintMetricsSummary
            {
                SprintId = sprint.Id,
                SprintKey = sprint.Key,
                SprintName = sprint.Name,
                State = sprint.State,
                Start = sprint.Start,
                End = sprint.End,
                Team = new Common.Application.Dtos.NavigationDto { Id = sprint.Team.Id, Key = sprint.Team.Key, Name = sprint.Team.Name },
                TotalWorkItems = metrics?.TotalWorkItems ?? 0,
                TotalStoryPoints = metrics?.TotalStoryPoints ?? 0,
                CompletedWorkItems = metrics?.CompletedWorkItems ?? 0,
                CompletedStoryPoints = metrics?.CompletedStoryPoints ?? 0,
                InProgressWorkItems = metrics?.InProgressWorkItems ?? 0,
                InProgressStoryPoints = metrics?.InProgressStoryPoints ?? 0,
                NotStartedWorkItems = metrics?.NotStartedWorkItems ?? 0,
                NotStartedStoryPoints = metrics?.NotStartedStoryPoints ?? 0,
                MissingStoryPointsCount = metrics?.MissingStoryPointsCount ?? 0,
                CycleTime = metrics?.CycleTime ?? CycleTimeSummary.Empty,
            };
        }).ToList();

        return Ok(new PlanningIntervalIterationMetricsResponse
        {
            IterationId = iteration.Id,
            IterationKey = iteration.Key,
            IterationName = iteration.Name,
            Start = iteration.Start,
            End = iteration.End,
            Category = iteration.Category,
            TeamCount = iteration.Sprints.Select(s => s.Team.Id).Distinct().Count(),
            SprintCount = iteration.Sprints.Count,
            TotalWorkItems = sprintMetricsSummaries.Sum(s => s.TotalWorkItems),
            TotalStoryPoints = sprintMetricsSummaries.Sum(s => s.TotalStoryPoints),
            CompletedWorkItems = sprintMetricsSummaries.Sum(s => s.CompletedWorkItems),
            CompletedStoryPoints = sprintMetricsSummaries.Sum(s => s.CompletedStoryPoints),
            InProgressWorkItems = sprintMetricsSummaries.Sum(s => s.InProgressWorkItems),
            InProgressStoryPoints = sprintMetricsSummaries.Sum(s => s.InProgressStoryPoints),
            NotStartedWorkItems = sprintMetricsSummaries.Sum(s => s.NotStartedWorkItems),
            NotStartedStoryPoints = sprintMetricsSummaries.Sum(s => s.NotStartedStoryPoints),
            MissingStoryPointsCount = sprintMetricsSummaries.Sum(s => s.MissingStoryPointsCount),
            CycleTime = CycleTimeSummary.Combine(sprintMetricsSummaries.Select(s => s.CycleTime)),
            SprintMetrics = sprintMetricsSummaries
        });
    }

    [HttpGet("{idOrKey}/iterations/{iterationIdOrKey}/backlog")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervals)]
    [OpenApiOperation("Get combined backlog for a PI iteration from all mapped sprints.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<SprintBacklogItemDto>>> GetIterationBacklog(string idOrKey, string iterationIdOrKey, CancellationToken cancellationToken)
    {
        // Get the iteration with its mapped sprints from Planning context
        var iterationSprints = await _dispatcher.Send(
            new GetPlanningIntervalIterationSprintsQuery(idOrKey, null),
            cancellationToken);

        if (iterationSprints is null)
            return NotFound();

        // Find the specific iteration
        var iterationKey = new IdOrKey(iterationIdOrKey);
        var iteration = iterationSprints.FirstOrDefault(i =>
            iterationKey.IsId ? i.Id == iterationKey.AsId : i.Key == iterationKey.AsKey);

        if (iteration is null)
            return NotFound();

        // Get combined backlog from Work context
        var sprintIds = iteration.Sprints.Select(s => s.Id).ToList();
        var backlog = await _dispatcher.Send(
            new GetSprintsBacklogQuery(sprintIds),
            cancellationToken);

        return Ok(backlog);
    }

    #endregion Iterations

    #region Objectives

    [HttpGet("{idOrKey}/objectives")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervalObjectives)]
    [OpenApiOperation("Get a list of planning interval teams.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PlanningIntervalObjectiveListDto>>> GetObjectives(string idOrKey, Guid? teamId, CancellationToken cancellationToken)
    {
        var objectives = await _dispatcher.Send(new GetPlanningIntervalObjectivesQuery(idOrKey, teamId), cancellationToken);

        return Ok(objectives);
    }

    [HttpGet("{idOrKey}/objectives/{objectiveIdOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervalObjectives)]
    [OpenApiOperation("Get a planning interval objective.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanningIntervalObjectiveDetailsDto>> GetObjective(string idOrKey, string objectiveIdOrKey, CancellationToken cancellationToken)
    {
        var objective = await _dispatcher.Send(new GetPlanningIntervalObjectiveQuery(idOrKey, objectiveIdOrKey), cancellationToken);

        return objective is not null
            ? Ok(objective)
            : NotFound();
    }

    [HttpPost("{id}/objectives")]
    [MustHavePermission(ApplicationAction.Manage, ApplicationResource.PlanningIntervalObjectives)]
    [OpenApiOperation("Create a planning interval objective.", "")]
    [ProducesResponseType(typeof(ObjectIdAndKey), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> CreateObjective(Guid id, [FromBody] CreatePlanningIntervalObjectiveRequest request, CancellationToken cancellationToken)
    {
        if (id != request.PlanningIntervalId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.PlanningIntervalId), HttpContext));

        var result = await _dispatcher.Send(request.ToCreatePlanningIntervalObjectiveCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetObjective), new { idOrKey = id, objectiveIdOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/objectives/{objectiveId}")]
    [MustHavePermission(ApplicationAction.Manage, ApplicationResource.PlanningIntervalObjectives)]
    [OpenApiOperation("Update a planning interval objective.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateObjective(Guid id, Guid objectiveId, [FromBody] UpdatePlanningIntervalObjectiveRequest request, CancellationToken cancellationToken)
    {
        if (id != request.PlanningIntervalId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.PlanningIntervalId), HttpContext));
        else if (objectiveId != request.ObjectiveId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(objectiveId), nameof(request.ObjectiveId), HttpContext));

        var result = await _dispatcher.Send(request.ToUpdatePlanningIntervalObjectiveCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/objectives/order")]
    [MustHavePermission(ApplicationAction.Manage, ApplicationResource.PlanningIntervalObjectives)]
    [OpenApiOperation("Update the order of planning interval objectives.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateObjectivesOrder(Guid id, [FromBody] UpdatePlanningIntervalObjectivesOrderRequest request, CancellationToken cancellationToken)
    {
        if (id != request.PlanningIntervalId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.PlanningIntervalId), HttpContext));

        var result = await _dispatcher.Send(new UpdatePlanningIntervalObjectivesOrderCommand(request.PlanningIntervalId, request.Objectives), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{idOrKey}/objectives/health-report")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervalObjectives)]
    [OpenApiOperation("Get a health report for planning interval objectives.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PlanningIntervalObjectiveHealthCheckDto>>> GetObjectivesHealthReport(string idOrKey, Guid? teamId, CancellationToken cancellationToken)
    {
        var objectives = await _dispatcher.Send(new GetPlanningIntervalObjectivesQuery(idOrKey, teamId), cancellationToken);
        if (objectives == null)
            return Ok(new List<PlanningIntervalObjectiveHealthCheckDto>());

        var objectiveHealthChecks = objectives.Select(PlanningIntervalObjectiveHealthCheckDto.Create).ToList();

        return Ok(objectiveHealthChecks);
    }

    [HttpGet("{idOrKey}/objectives/{objectiveIdOrKey}/work-items")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervalObjectives)]
    [OpenApiOperation("Get work items for an objective.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkItemsSummaryDto>> GetObjectiveWorkItems(string idOrKey, string objectiveIdOrKey, CancellationToken cancellationToken)
    {
        var objectiveId = await _dispatcher.Send(new CheckPlanningIntervalObjectiveExistsQuery(idOrKey, objectiveIdOrKey), cancellationToken);
        if (objectiveId is null)
            return NotFound();

        var workItemsSummary = await _dispatcher.Send(new GetExternalObjectWorkItemsQuery(objectiveId.Value), cancellationToken);

        if (workItemsSummary is null)
            return NotFound();

        if (workItemsSummary.WorkItems.Count > 0)
            workItemsSummary.WorkItems = [.. workItemsSummary.WorkItems.OrderBy(w => w.StackRank)];

        return Ok(workItemsSummary);
    }

    [HttpGet("{idOrKey}/objectives/{objectiveIdOrKey}/work-items/metrics")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervalObjectives)]
    [OpenApiOperation("Get metrics for the work items linked to an objective.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<WorkItemProgressDailyRollupDto>>> GetObjectiveWorkItemMetrics(string idOrKey, string objectiveIdOrKey, CancellationToken cancellationToken)
    {
        var objectiveId = await _dispatcher.Send(new CheckPlanningIntervalObjectiveExistsQuery(idOrKey, objectiveIdOrKey), cancellationToken);
        if (objectiveId is null)
            return NotFound();

        var planningInterval = await _dispatcher.Send(new GetPlanningIntervalQuery(idOrKey), cancellationToken);

        var today = _dateTimeProvider.Now.ToDateOnly();
        var piEnd = planningInterval!.End.ToDateOnly();
        // get the min of today and the end of the PI
        var end = today < piEnd ? today : piEnd;

        var dailyRollup = await _dispatcher.Send(new GetExternalObjectWorkItemMetricsQuery(objectiveId.Value, planningInterval!.Start.ToDateOnly(), end), cancellationToken);

        if (dailyRollup is null)
            return NotFound();

        return Ok(dailyRollup);
    }

    [HttpPost("{id}/objectives/{objectiveId}/work-items")]
    [MustHavePermission(ApplicationAction.Manage, ApplicationResource.PlanningIntervalObjectives)]
    [OpenApiOperation("Manage objective work items.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ManageObjectiveWorkItems(Guid id, Guid objectiveId, [FromBody] ManagePlanningIntervalObjectiveWorkItemsRequest request, CancellationToken cancellationToken)
    {
        if (id != request.PlanningIntervalId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.PlanningIntervalId), HttpContext));
        else if (objectiveId != request.ObjectiveId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(objectiveId), nameof(request.ObjectiveId), HttpContext));

        var confirmedObjectiveId = await _dispatcher.Send(new CheckPlanningIntervalObjectiveExistsQuery(new IdOrKey(id), new IdOrKey(objectiveId)), cancellationToken);
        if (confirmedObjectiveId is null)
            return NotFound();

        var result = await _dispatcher.Send(request.ToManageExternalObjectWorkItemsCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/objectives/import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.PlanningIntervalObjectives)]
    [OpenApiOperation("Import objectives for a planning interval from a csv file.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ImportObjectives(Guid id, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            var importedObjectives = _csvService.ReadCsv<ImportPlanningIntervalObjectivesRequest>(file.OpenReadStream());

            List<ImportPlanningIntervalObjectiveDto> objectives = [];
            var validator = new ImportPlanningIntervalObjectivesRequestValidator();
            foreach (var objective in importedObjectives)
            {
                // TODO: allow importing of objectives for multiple PIs at once
                if (id != objective.PlanningIntervalId)
                    return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(objective.PlanningIntervalId), HttpContext));

                var validationResults = await validator.ValidateAsync(objective, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        if (error.PropertyName != "RecordId")
                        {
                            error.ErrorMessage = $"{error.ErrorMessage} (Record Id: {objective.ImportId})";
                            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                        }
                    }
                    return UnprocessableEntity(validationResults);
                }
                else
                {
                    objectives.Add(objective.ToImportPlanningIntervalObjectiveDto());
                }
            }

            if (!objectives.Any())
                return BadRequest("No PI objectives imported.");

            var result = await _dispatcher.Send(new ImportPlanningIntervalObjectivesCommand(objectives), cancellationToken);

            return result.IsSuccess
                ? NoContent()
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            // TODO: Convert this to a validation problem details
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.ToString(), HttpContext));
        }
    }

    [HttpDelete("{id}/objectives/{objectiveId}")]
    [MustHavePermission(ApplicationAction.Manage, ApplicationResource.PlanningIntervalObjectives)]
    [OpenApiOperation("Delete a planning interval objective.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteObjective(Guid id, Guid objectiveId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeletePlanningIntervalObjectiveCommand(id, objectiveId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("objective-statuses")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervalObjectives)]
    [OpenApiOperation("Get a list of all PI objective statuses.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PlanningIntervalObjectiveStatusDto>>> GetObjectiveStatuses(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetPlanningIntervalObjectiveStatusesQuery(), cancellationToken);
        return Ok(items.OrderBy(c => c.Order));
    }

    #endregion Objectives

    #region Objective Health Checks

    [HttpGet("{id}/objectives/{objectiveId}/health-checks")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervalObjectives)]
    [OpenApiOperation("Get the health check history for a planning interval objective.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PlanningIntervalObjectiveHealthCheckDetailsDto>>> GetObjectiveHealthChecks(Guid id, Guid objectiveId, CancellationToken cancellationToken)
    {
        var healthChecks = await _dispatcher.Send(new GetPlanningIntervalObjectiveHealthChecksQuery(objectiveId), cancellationToken);
        return Ok(healthChecks);
    }

    [HttpGet("{id}/objectives/{objectiveId}/health-checks/{healthCheckId}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervalObjectives)]
    [OpenApiOperation("Get a planning interval objective health check by id.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanningIntervalObjectiveHealthCheckDetailsDto>> GetObjectiveHealthCheck(Guid id, Guid objectiveId, Guid healthCheckId, CancellationToken cancellationToken)
    {
        var healthCheck = await _dispatcher.Send(new GetPlanningIntervalObjectiveHealthCheckQuery(objectiveId, healthCheckId), cancellationToken);

        return healthCheck is not null
            ? Ok(healthCheck)
            : NotFound();
    }

    [HttpPost("{id}/objectives/{objectiveId}/health-checks")]
    [MustHavePermission(ApplicationAction.Manage, ApplicationResource.PlanningIntervalObjectives)]
    [OpenApiOperation("Create a planning interval objective health check.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult> CreateObjectiveHealthCheck(Guid id, Guid objectiveId, [FromBody] CreatePlanningIntervalObjectiveHealthCheckRequest request, CancellationToken cancellationToken)
    {
        if (objectiveId != request.PlanningIntervalObjectiveId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(objectiveId), nameof(request.PlanningIntervalObjectiveId), HttpContext));

        var result = await _dispatcher.Send(request.ToCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetObjectiveHealthCheck), new { id, objectiveId, healthCheckId = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/objectives/{objectiveId}/health-checks/{healthCheckId}")]
    [MustHavePermission(ApplicationAction.Manage, ApplicationResource.PlanningIntervalObjectives)]
    [OpenApiOperation("Update a planning interval objective health check.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PlanningIntervalObjectiveHealthCheckDetailsDto>> UpdateObjectiveHealthCheck(Guid id, Guid objectiveId, Guid healthCheckId, [FromBody] UpdatePlanningIntervalObjectiveHealthCheckRequest request, CancellationToken cancellationToken)
    {
        if (objectiveId != request.PlanningIntervalObjectiveId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(objectiveId), nameof(request.PlanningIntervalObjectiveId), HttpContext));
        else if (healthCheckId != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(healthCheckId), nameof(request.Id), HttpContext));

        var result = await _dispatcher.Send(request.ToCommand(), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/objectives/{objectiveId}/health-checks/{healthCheckId}")]
    [MustHavePermission(ApplicationAction.Manage, ApplicationResource.PlanningIntervalObjectives)]
    [OpenApiOperation("Delete a planning interval objective health check.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteObjectiveHealthCheck(Guid id, Guid objectiveId, Guid healthCheckId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeletePlanningIntervalObjectiveHealthCheckCommand(objectiveId, healthCheckId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Objective Health Checks

    #region Risks

    [HttpGet("{idOrKey}/risks")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PlanningIntervals)]
    [OpenApiOperation("Get planning interval risks. The default value for includeClosed is false.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<RiskListDto>>> GetRisks(string idOrKey, bool? includeClosed, Guid? teamId, CancellationToken cancellationToken)
    {
        includeClosed ??= false;
        var risks = await _dispatcher.Send(new GetRisksByPlanningIntervalQuery(idOrKey, includeClosed.Value, teamId), cancellationToken);

        return Ok(risks);
    }

    #endregion Risks
}
