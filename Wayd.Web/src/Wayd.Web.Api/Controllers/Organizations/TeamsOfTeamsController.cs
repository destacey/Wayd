using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Organization.Application.EmployeeCapacities;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Organization.Application.Models;
using Wayd.Organization.Application.Teams.Dtos;
using TeamsMemberCommands = Wayd.Organization.Application.Teams.Commands;
using TeamsMemberQueries = Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.TeamsOfTeams.Commands;
using Wayd.Organization.Application.TeamsOfTeams.Dtos;
using Wayd.Organization.Application.TeamsOfTeams.Queries;
using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Planning.Application.Risks.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Organizations;
using Wayd.Web.Api.Models.Organizations.TeamOfTeams;
using Wayd.Web.Api.Models.Organizations.Teams;
using Wayd.Web.Api.Models.Organizations.TeamsOfTeams;
using Wayd.Web.Api.Models.Planning.Risks;
using Wayd.Work.Application.WorkTeams.Dtos;

namespace Wayd.Web.Api.Controllers.Organizations;

[Route("api/organization/teams-of-teams")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Teams, "wayd-teams")]
public class TeamsOfTeamsController : ControllerBase
{
    private readonly ILogger<TeamsOfTeamsController> _logger;
    private readonly IDispatcher _dispatcher;

    public TeamsOfTeamsController(ILogger<TeamsOfTeamsController> logger, IDispatcher dispatcher)
    {
        _logger = logger;
        _dispatcher = dispatcher;
    }

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get a list of teams of teams in the organization.")]
    [McpTool("TeamsOfTeams_GetList", "List teams of teams")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TeamOfTeamsListDto>>> GetList(CancellationToken cancellationToken, bool includeInactive = false)
    {
        var teams = await _dispatcher.Send(new GetTeamOfTeamsListQuery(includeInactive), cancellationToken);
        return Ok(teams.OrderBy(e => e.Name));
    }

    [HttpGet("{id}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get team of teams details using the key.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamOfTeamsDetailsDto>> GetById(int id)
    {
        var team = await _dispatcher.Send(new GetTeamOfTeamsQuery(id));

        return team is not null
            ? Ok(team)
            : NotFound();
    }

    [HttpGet("{idOrKey}/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get activity history for the team of teams.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(new GetTeamOfTeamsActivitiesQuery(new IdOrKey(idOrKey), page, pageSize), cancellationToken);

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpGet("{idOrCode}/work-distribution")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [EndpointSummary("Report where the completed work of a team of teams, and every team beneath it, went.")]
    [EndpointDescription(
        "Groups the Requirement-tier work items completed between `from` and `to` (yyyy-MM-dd, inclusive, UTC, max 366 days) by portfolio, program, project, strategic theme or work type. Each team's work rolls up to the parent it had on the day the work was done. Measures: Count, StoryPoints (only teams sizing in story points), or TeamEffort (each team's split in its own sizing method, StoryPoints, Effort, Size or Count, as on the day the work was done, combined by share of completed items; recommended for teams of teams). Work with no project is its own group.")]
    [McpTool("TeamsOfTeams_GetWorkDistribution", "Report team of teams work distribution")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TeamWorkDistributionDto>> GetWorkDistribution(
        string idOrCode,
        [FromQuery] GetTeamWorkDistributionRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.TryToQuery(idOrCode, out var query, out var error))
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(error!, HttpContext));

        var result = await _dispatcher.Send(query!, cancellationToken);

        return result.IsFailure
            ? BadRequest(result.ToBadRequestObject(HttpContext))
            : result.Value is not null
                ? Ok(result.Value)
                : NotFound();
    }

    [HttpGet("{id:guid}/capacity")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary(
        "Get the capacity of a team of teams over a date range, from the time people are allocated to it.")]
    [EndpointDescription(
        "Returns, for `from` to `to` (yyyy-MM-dd, inclusive, max 731 days), the `workingDays` in the range and each person allocated to the team of teams with their person-days: `allocatedPersonDays` in every role, `deliveryPersonDays` in roles that count toward delivery capacity, and `elsewherePersonDays` allocated to other teams on the same days. One person-day is one person allocated in full for one working day. `roles` breaks the allocated time down by team member role, each line saying whether it counts toward delivery capacity, and adds up to `allocatedPersonDays`. Each person carries their `allocations` to this team with the `personDays` each is worth, and their `elsewhereAllocations` to other teams in the range. It counts only people allocated to the team of teams directly, over the system default working week and holidays; `capacity/rollup` adds the teams beneath it. `canManage` says whether the caller may change the allocations. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamCapacityDto>> GetCapacity(Guid id, [FromQuery] string? from, [FromQuery] string? to, CancellationToken cancellationToken)
    {
        if (!IsoDateQuery.TryParse(from, out var firstDay) || !IsoDateQuery.TryParse(to, out var lastDay))
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(IsoDateQuery.FormatError, HttpContext));

        if (firstDay is null || lastDay is null)
            return BadRequest(ProblemDetailsExtensions.ForBadRequest("Both from and to dates are required.", HttpContext));

        var capacity = await _dispatcher.Send(new GetTeamCapacityQuery(id, TeamType.TeamOfTeams, firstDay.Value, lastDay.Value), cancellationToken);

        return capacity is not null
            ? Ok(capacity)
            : NotFound();
    }

    [HttpGet("{id:guid}/capacity/rollup")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get the capacity of a team of teams over a date range with the teams beneath it rolled up.")]
    [EndpointDescription(
        "Returns, for `from` to `to` (yyyy-MM-dd, inclusive, max 731 days), the time allocated to the team of teams directly plus the time allocated to every team beneath it, at any depth. `allocatedPersonDays` and `deliveryPersonDays` are the totals; `directAllocatedPersonDays` and `directDeliveryPersonDays` are the team of teams' own part, which is what the `capacity` endpoint returns in detail. `children` is the tree of teams beneath it. Each has the `periods` of the range it counts for (the days its membership of its parent covers, less any its parent was itself out of the hierarchy), its own `workingDays` within them, the `people` allocated to it with each allocation's `personDays` over those days, its direct figures, and its totals including its own `children`. A team is counted over its own working week and holidays, a team of teams over the system defaults, and a team with nothing allocated is still listed. A team that moved between two parents in the range appears under each for its days there. An allocation counts once, on the team it was made to, so a person allocated at two levels has time at each and none of it twice; `people` counts that person once. `roles` breaks the total down by team member role and adds up to `allocatedPersonDays`. Nothing is stored and nothing can be changed here: change a team's allocations through that team. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamCapacityRollupDto>> GetCapacityRollup(Guid id, [FromQuery] string? from, [FromQuery] string? to, CancellationToken cancellationToken)
    {
        if (!IsoDateQuery.TryParse(from, out var firstDay) || !IsoDateQuery.TryParse(to, out var lastDay))
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(IsoDateQuery.FormatError, HttpContext));

        if (firstDay is null || lastDay is null)
            return BadRequest(ProblemDetailsExtensions.ForBadRequest("Both from and to dates are required.", HttpContext));

        var rollup = await _dispatcher.Send(new GetTeamOfTeamsCapacityRollupQuery(id, firstDay.Value, lastDay.Value), cancellationToken);

        return rollup is not null
            ? Ok(rollup)
            : NotFound();
    }

    [HttpGet("{id:guid}/capacity/report")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Report the capacity of a team of teams and every team beneath it over a date range.")]
    [EndpointDescription(
        "Returns, for `from` to `to` (yyyy-MM-dd, inclusive, max 731 days), one `lines` entry per person, team member role and team: the team of teams itself and every team beneath it at any depth, each counted only for the days it was in the hierarchy. A line's `total` is in person-days: `allocatedPersonDays` over the team's working days, the part lost to `holidayPersonDays` and `timeAwayPersonDays`, what is left as `availablePersonDays`, and the part of that in roles that count toward delivery capacity as `deliveryPersonDays`. `interval` (`Week` or `Month`) also splits each line into `buckets`, in the order of the report's `buckets`. `teams` lists the team of teams first (`nodeId` 0) and each team beneath it with its `parentNodeId`; a team that moved between two parents appears under each. `people` gives each person's time over the system default working days that the report does not count: `outsidePersonDays` allocated elsewhere and `unallocatedPersonDays` allocated nowhere. An allocation counts once, on the team it was made to, so every grouping of the lines gives `total`. Time away is reported as time lost, never why. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamCapacityReportDto>> GetCapacityReport(Guid id, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] CapacityReportInterval? interval, CancellationToken cancellationToken)
    {
        if (!IsoDateQuery.TryParse(from, out var firstDay) || !IsoDateQuery.TryParse(to, out var lastDay))
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(IsoDateQuery.FormatError, HttpContext));

        if (firstDay is null || lastDay is null)
            return BadRequest(ProblemDetailsExtensions.ForBadRequest("Both from and to dates are required.", HttpContext));

        var report = await _dispatcher.Send(new GetTeamCapacityReportQuery(id, TeamType.TeamOfTeams, firstDay.Value, lastDay.Value, interval), cancellationToken);

        return report is not null
            ? Ok(report)
            : NotFound();
    }

    [HttpGet("{id:guid}/capacity/plan")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get the capacity of a team of teams over a date range day by day, to plan against.")]
    [EndpointDescription(
        "Returns, for `from` to `to` (yyyy-MM-dd, inclusive, max 366 days), the `capacity` the `capacity` endpoint returns for the same range, the `days` of the system default working week in the range, and for each person allocated to it their time day by day. Each person has a `rows` entry per team member role with the `days` that role has time allocated: the `percent` and the `personDays` it is worth after the holidays the person takes and their time away. Their own `days` list the working days on which something else decides their time: `isHoliday` in the calendar they take, `percentAway` (never why), and `elsewherePercent` allocated to other teams. A day left out of a list has none of it. Nothing is stored per day: change a day or a week through the change allocations endpoint, as a change over that range. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamCapacityPlanDto>> GetCapacityPlan(Guid id, [FromQuery] string? from, [FromQuery] string? to, CancellationToken cancellationToken)
    {
        if (!IsoDateQuery.TryParse(from, out var firstDay) || !IsoDateQuery.TryParse(to, out var lastDay))
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(IsoDateQuery.FormatError, HttpContext));

        if (firstDay is null || lastDay is null)
            return BadRequest(ProblemDetailsExtensions.ForBadRequest("Both from and to dates are required.", HttpContext));

        var plan = await _dispatcher.Send(new GetTeamCapacityPlanQuery(id, TeamType.TeamOfTeams, firstDay.Value, lastDay.Value), cancellationToken);

        return plan is not null
            ? Ok(plan)
            : NotFound();
    }

    [HttpGet("{id:guid}/capacity/proposal")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Propose allocations for a team of teams from its members, without saving anything.")]
    [EndpointDescription(
        "Returns one row per active member and active role they hold on the team of teams, for allocations starting on `from` (yyyy-MM-dd). A row has no `end` unless the member already has a later allocation to it, in which case it ends the day before so that allocation is untouched; a member with time on it on `from` has no rows. `remainingPercent` is what is left of the member's time across all teams: 100 less their most allocated day from `from` to `end`. `percent` is that remainder split evenly between the member's roles. A member with nothing left is returned at 0, not left out. Nothing is saved: send the rows to keep to the change allocations endpoint. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamAllocationProposalDto>> GetAllocationProposal(Guid id, [FromQuery] string? from, CancellationToken cancellationToken)
    {
        if (!IsoDateQuery.TryParse(from, out var firstDay))
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(IsoDateQuery.FormatError, HttpContext));

        if (firstDay is null)
            return BadRequest(ProblemDetailsExtensions.ForBadRequest("The from date is required.", HttpContext));

        var proposal = await _dispatcher.Send(new GetTeamAllocationProposalQuery(id, TeamType.TeamOfTeams, firstDay.Value), cancellationToken);

        return proposal is not null
            ? Ok(proposal)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Teams)]
    [EndpointSummary("Create a team of teams.")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create(CreateTeamOfTeamsRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateTeamOfTeamsCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Key }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Update a team of teams.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, UpdateTeamOfTeamsRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateTeamOfTeamsCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/deactivate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Deactivate a team of teams.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Deactivate(Guid id, [FromBody] DeactivateTeamOfTeamsRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToDeactivateTeamOfTeamsCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #region Team Memberships

    [HttpGet("{id}/team-memberships")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get parent team memberships.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TeamMembershipDto>>> GetTeamMemberships(Guid id, CancellationToken cancellationToken)
    {
        var memberships = await _dispatcher.Send(new GetTeamMembershipsQuery(id), cancellationToken);

        return Ok(memberships);
    }

    [HttpPost("{id}/team-memberships")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Add a parent team membership.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> AddTeamMembership(Guid id, [FromBody] AddTeamMembershipRequest request, CancellationToken cancellationToken)
    {
        if (id != request.TeamId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToTeamOfTeamsAddParentTeamMembershipCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/team-memberships/{teamMembershipId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Update a team membership.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateTeamMembership(Guid id, Guid teamMembershipId, [FromBody] UpdateTeamMembershipRequest request, CancellationToken cancellationToken)
    {
        if (id != request.TeamId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.TeamId), HttpContext));
        else if (teamMembershipId != request.TeamMembershipId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(teamMembershipId), nameof(request.TeamMembershipId), HttpContext));

        var result = await _dispatcher.Send(request.ToTeamOfTeamsUpdateTeamMembershipCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/team-memberships/{teamMembershipId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Remove a parent team membership.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RemoveTeamMembership(Guid id, Guid teamMembershipId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemoveTeamMembershipCommand(id, teamMembershipId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Team Memberships

    #region Operating Models

    [HttpGet("{id}/operating-models")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get the operating model history for a team of teams.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TeamOfTeamsOperatingModelDetailsDto>>> GetOperatingModels(Guid id, CancellationToken cancellationToken)
    {
        var teamExists = await _dispatcher.Send(new TeamOfTeamsExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var models = await _dispatcher.Send(new GetTeamOfTeamsOperatingModelsQuery(id), cancellationToken);

        return Ok(models);
    }

    [HttpGet("{id}/operating-models/defaults")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get the values a new operating model for a team of teams is pre-filled with.")]
    [EndpointDescription("The time zone is the one its parent team of teams had in effect on the start date (yyyy-MM-dd), else the system default.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperatingModelDefaultsDto>> GetOperatingModelDefaults(Guid id, [FromQuery] string startDate, CancellationToken cancellationToken)
    {
        if (!IsoDateQuery.TryParse(startDate, out var parsedStartDate) || parsedStartDate is null)
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(IsoDateQuery.FormatError, HttpContext));

        var teamExists = await _dispatcher.Send(new TeamOfTeamsExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var defaults = await _dispatcher.Send(new TeamsMemberQueries.GetOperatingModelDefaultsQuery(id, parsedStartDate.Value), cancellationToken);

        return defaults is not null
            ? Ok(defaults)
            : NotFound();
    }

    [HttpGet("{id}/operating-models/{operatingModelId}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get a specific operating model for a team of teams.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamOfTeamsOperatingModelDetailsDto>> GetOperatingModel(Guid id, Guid operatingModelId, CancellationToken cancellationToken)
    {
        var operatingModel = await _dispatcher.Send(new GetTeamOfTeamsOperatingModelQuery(id, operatingModelId), cancellationToken);

        return operatingModel is not null
            ? Ok(operatingModel)
            : NotFound();
    }

    [HttpPost("{id}/operating-models")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Set a new operating model for a team of teams.")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> SetOperatingModel(Guid id, [FromBody] SetTeamOfTeamsOperatingModelRequest request, CancellationToken cancellationToken)
    {
        var teamExists = await _dispatcher.Send(new TeamOfTeamsExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var result = await _dispatcher.Send(request.ToSetTeamOfTeamsOperatingModelCommand(id), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetOperatingModel), new { id, operatingModelId = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/operating-models/{operatingModelId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Correct an existing operating model for a team of teams.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateOperatingModel(Guid id, Guid operatingModelId, [FromBody] UpdateTeamOfTeamsOperatingModelRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdateTeamOfTeamsOperatingModelCommand(id, operatingModelId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/operating-models/{operatingModelId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Delete the current operating model from a team of teams.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteOperatingModel(Guid id, Guid operatingModelId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteTeamOfTeamsOperatingModelCommand(id, operatingModelId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Operating Models

    #region Risks

    [HttpGet("{id}/risks")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get team risks.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RiskListDto>>> GetRisks(Guid id, CancellationToken cancellationToken, bool includeClosed = false)
    {
        var teamExists = await _dispatcher.Send(new TeamOfTeamsExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var risks = await _dispatcher.Send(new GetRisksQuery(id, includeClosed), cancellationToken);

        return Ok(risks);
    }

    [HttpGet("{id}/risks/{riskIdOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get a team of teams risk by Id.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RiskDetailsDto>> GetRiskById(Guid id, string riskIdOrKey, CancellationToken cancellationToken)
    {
        var teamExists = await _dispatcher.Send(new TeamOfTeamsExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var risk = await _dispatcher.Send(new GetRiskQuery(riskIdOrKey), cancellationToken);

        return risk is not null && risk.Team?.Id == id
            ? Ok(risk)
            : NotFound();
    }

    [HttpPost("{id}/risks")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Create a risk for a team of teams.")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> CreateRisk(Guid id, [FromBody] CreateRiskRequest request, CancellationToken cancellationToken)
    {
        if (id != request.TeamId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.TeamId), HttpContext));

        var teamExists = await _dispatcher.Send(new TeamOfTeamsExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var result = await _dispatcher.Send(request.ToCreateRiskCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRiskById), new { id, riskIdOrKey = result.Value.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/risks/{riskId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Update a team of teams risk.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateRisk(Guid id, Guid riskId, [FromBody] UpdateRiskRequest request, CancellationToken cancellationToken)
    {
        if (id != request.TeamId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.TeamId), HttpContext));
        else if (riskId != request.RiskId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(riskId), nameof(request.RiskId), HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateRiskCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Risks

    #region Team Members

    [HttpGet("{id}/members")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get the members of a team of teams.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamMemberDto>>> GetMembers(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _dispatcher.Send(new TeamsMemberQueries.GetTeamMembersQuery(id), cancellationToken));
    }

    [HttpPost("{id}/members")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Add a member to a team of teams.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AddMember(Guid id, [FromBody] AddTeamMemberRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new TeamsMemberCommands.AddTeamMemberCommand(id, request.EmployeeId, request.RoleIds), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/members/{employeeId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Update a team of teams member's roles.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateMember(Guid id, Guid employeeId, [FromBody] UpdateTeamMemberRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new TeamsMemberCommands.UpdateTeamMemberCommand(id, employeeId, request.RoleIds), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/members/{employeeId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Remove a member from a team of teams.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RemoveMember(Guid id, Guid employeeId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new TeamsMemberCommands.RemoveTeamMemberCommand(id, employeeId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Team Members
}
