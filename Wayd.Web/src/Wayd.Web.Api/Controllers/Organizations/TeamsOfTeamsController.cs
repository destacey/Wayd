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
    [EndpointDescription("Takes the integer `key` (the parameter is named `id`), not the UUID; TeamsOfTeams_GetList returns both.")]
    [McpTool("TeamsOfTeams_GetTeamOfTeams", "Get team of teams")]
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
    [EndpointSummary(
        "Get a team of teams' activity history, newest first: its creation, detail changes, activation and deactivation, members joining, leaving or changing roles (by employee and role id), its own membership in a parent team of teams being added, re-dated or removed, and operating models being set, corrected or removed.")]
    [EndpointDescription(
        "Takes the UUID or integer key. Each entry has a `category` (Created, Updated, ScheduleChanged, StatusChanged, StateChanged, Health, Removed, Baseline), an `actorKind` (User, System, Import, Sync, Anonymous) with the acting `employee` when there is one, a `timestamp`, a one-line `summary`, and a `payload`: the event's fields as a JSON string. A change carries both ends, the value before and after. People in a payload are employee ids, not user ids. A Baseline entry marks where tracking began for a record that already existed; nothing before it was recorded. Paged: the response carries `totalCount` and `hasNextPage`.")]
    [McpTool("TeamsOfTeams_GetActivities", "Get team of teams activity history")]
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
        "Takes the team of teams' UUID `id`. Returns, for `from` to `to` (yyyy-MM-dd, inclusive, max 731 days), the `workingDays` in the range and each person allocated to the team of teams with their person-days: `allocatedPersonDays` in every role, `deliveryPersonDays` in roles that count toward delivery capacity, and `elsewherePersonDays` allocated to other teams on the same days. One person-day is one person allocated in full for one working day. `roles` breaks the allocated time down by team member role, each line saying whether it counts toward delivery capacity, and adds up to `allocatedPersonDays`. Each person carries their `allocations` to this team with the `personDays` each is worth, and their `elsewhereAllocations` to other teams in the range. It counts only people allocated to the team of teams directly, over the system default working week and holidays; `capacity/rollup` (TeamsOfTeams_GetCapacityRollup) adds the teams beneath it. `canManage` says whether the caller may change the allocations, which is done per person with Employees_ChangeAllocations. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [McpTool("TeamsOfTeams_GetCapacity", "Get team of teams capacity")]
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
        "Takes the team of teams' UUID `id`. Returns, for `from` to `to` (yyyy-MM-dd, inclusive, max 731 days), the time allocated to the team of teams directly plus the time allocated to every team beneath it, at any depth. `allocatedPersonDays` and `deliveryPersonDays` are the totals; `directAllocatedPersonDays` and `directDeliveryPersonDays` are the team of teams' own part, which is what the `capacity` endpoint returns in detail. `children` is the tree of teams beneath it. Each has the `periods` of the range it counts for (the days its membership of its parent covers, less any its parent was itself out of the hierarchy), its own `workingDays` within them, the `people` allocated to it with each allocation's `personDays` over those days, its direct figures, and its totals including its own `children`. A team is counted over its own working week and holidays, a team of teams over the system defaults, and a team with nothing allocated is still listed. A team that moved between two parents in the range appears under each for its days there. An allocation counts once, on the team it was made to, so a person allocated at two levels has time at each and none of it twice; `people` counts that person once. `roles` breaks the total down by team member role and adds up to `allocatedPersonDays`. Nothing is stored and nothing can be changed here: change a team's allocations through that team. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [McpTool("TeamsOfTeams_GetCapacityRollup", "Get team of teams capacity rollup")]
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
        "Takes the team of teams' UUID `id`. Returns, for `from` to `to` (yyyy-MM-dd, inclusive, max 731 days), one `lines` entry per person, team member role and team: the team of teams itself and every team beneath it at any depth, each counted only for the days it was in the hierarchy. A line's `total` is in person-days: `allocatedPersonDays` over the team's working days, the part lost to `holidayPersonDays` and `timeAwayPersonDays`, what is left as `availablePersonDays`, and the part of that in roles that count toward delivery capacity as `deliveryPersonDays`. `interval` (`Week` or `Month`) also splits each line into `buckets`, in the order of the report's `buckets`. `teams` lists the team of teams first (`nodeId` 0) and each team beneath it with its `parentNodeId`; a team that moved between two parents appears under each. `people` gives each person's time over the system default working days that the report does not count: `outsidePersonDays` allocated elsewhere and `unallocatedPersonDays` allocated nowhere. An allocation counts once, on the team it was made to, so every grouping of the lines gives `total`. Time away is reported as time lost, never why. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [McpTool("TeamsOfTeams_GetCapacityReport", "Report team of teams capacity")]
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
        "Takes the team of teams' UUID `id`. Returns, for `from` to `to` (yyyy-MM-dd, inclusive, max 366 days), the `capacity` the `capacity` endpoint (TeamsOfTeams_GetCapacity) returns for the same range, the `days` of the system default working week in the range, and for each person allocated to it their time day by day. Each person has a `rows` entry per team member role with the `days` that role has time allocated: the `percent` and the `personDays` it is worth after the holidays the person takes and their time away. Their own `days` list the working days on which something else decides their time: `isHoliday` in the calendar they take, `percentAway` (never why), and `elsewherePercent` allocated to other teams. A day left out of a list has none of it. Nothing is stored per day: change a day or a week through the change allocations endpoint (Employees_ChangeAllocations), as a change over that range. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [McpTool("TeamsOfTeams_GetCapacityPlan", "Get team of teams capacity plan")]
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
        "Takes the team of teams' UUID `id`. Returns one row per active member and active role they hold on the team of teams, for allocations starting on `from` (yyyy-MM-dd). A row has no `end` unless the member already has a later allocation to it, in which case it ends the day before so that allocation is untouched; a member with time on it on `from` has no rows. `remainingPercent` is what is left of the member's time across all teams: 100 less their most allocated day from `from` to `end`. `percent` is that remainder split evenly between the member's roles. A member with nothing left is returned at 0, not left out. Nothing is saved: send the rows to keep to the change allocations endpoint (Employees_ChangeAllocations). Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [McpTool("TeamsOfTeams_GetAllocationProposal", "Propose team of teams allocations")]
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
    [EndpointDescription(
        "`name` and `code` are each unique across teams and teams of teams; `code` is 2 to 10 uppercase letters and digits. `activeDate` (yyyy-MM-dd) is the day it starts. It starts with an operating model in the system default time zone. To nest it under another team of teams, call TeamsOfTeams_AddTeamMembership; to put teams under it, call Teams_AddTeamMembership on each team. Returns the new team of teams' `id` (UUID) and `key` (integer).")]
    [McpTool("TeamsOfTeams_Create", "Create team of teams", Destructive = false)]
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
    [EndpointSummary("Update a team of teams' name, code and description.")]
    [EndpointDescription(
        "**This is a whole-record overwrite of those three fields: an omitted description is cleared.** Read it first with TeamsOfTeams_GetTeamOfTeams and pass back what should stay. Takes the UUID `id`, which must match the body's `id`. `code` is 2 to 10 uppercase letters and digits.")]
    [McpTool("TeamsOfTeams_Update", "Update team of teams")]
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
    [EndpointDescription(
        "Takes the UUID `id`, which must match the body's `id`, and the `inactiveDate` (yyyy-MM-dd), which must be after its active date. Refused when it is already inactive, when its own latest membership in a parent has no end or ends after `inactiveDate`, or when the latest membership of a team beneath it has no end or ends after `inactiveDate`: end those memberships first. No tool reactivates it, so confirm with the user before calling.")]
    [McpTool("TeamsOfTeams_Deactivate", "Deactivate team of teams")]
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
    [EndpointSummary("Get a team of teams' memberships: in its parent, and of the teams beneath it.")]
    [EndpointDescription(
        "Takes the UUID `id`. Lists its own memberships in a parent team of teams first (where it is the `child`), then the memberships of the teams and teams of teams directly beneath it (where it is the `parent`), past, current and future. Each has its own `id` (the `teamMembershipId` the update and remove tools take), `start` and `end` (yyyy-MM-dd; no end is open-ended), and a `state` (Past, Active or Future) as of today. Change a child's membership through the child: Teams_UpdateTeamMembership for a team, TeamsOfTeams_UpdateTeamMembership for a team of teams. Returns 404 for an unknown team of teams.")]
    [McpTool("TeamsOfTeams_GetTeamMemberships", "List team of teams memberships")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TeamMembershipDto>>> GetTeamMemberships(Guid id, CancellationToken cancellationToken)
    {
        var memberships = await _dispatcher.Send(new GetTeamMembershipsQuery(id), cancellationToken);

        return memberships is null
            ? NotFound()
            : Ok(memberships);
    }

    [HttpPost("{id}/team-memberships")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Nest a team of teams under another team of teams for a date range.")]
    [EndpointDescription(
        "Takes the UUID `id` of the team of teams being nested, which must match the body's `teamId`, and the parent's UUID in `parentTeamId`. `start` and optional `end` are yyyy-MM-dd; no `end` is open-ended. Both must be active, the range must not overlap its other parent memberships, and the parent cannot be one of its own descendants. To put a team (not a team of teams) under it, use Teams_AddTeamMembership.")]
    [McpTool("TeamsOfTeams_AddTeamMembership", "Nest team of teams under another", Destructive = false)]
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
    [EndpointSummary("Change the dates of a team of teams' membership in its parent.")]
    [EndpointDescription(
        "Takes the UUID `id` of the team of teams that is the child and the membership's `teamMembershipId`, each of which must match the body. **Both dates are overwritten: an omitted `end` makes the membership open-ended**, so pass the current `end` back to keep it. Both must be active, and the new range must not overlap its other parent memberships.")]
    [McpTool("TeamsOfTeams_UpdateTeamMembership", "Change team of teams membership dates")]
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
    [EndpointSummary("Remove a team of teams' membership in its parent.")]
    [EndpointDescription(
        "Takes the UUID `id` of the team of teams that is the child and the membership's `teamMembershipId`. The membership is deleted for its whole range, so it drops out of the parent's rollups for past dates too; to record that it left, end the membership with TeamsOfTeams_UpdateTeamMembership instead. Both must be active.")]
    [McpTool("TeamsOfTeams_RemoveTeamMembership", "Remove team of teams from its parent")]
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
    [EndpointDescription(
        "Takes the UUID `id`. A team of teams' operating model holds only the IANA `timeZone` its own rollups count days in, from `start` to `end` (none while current); `isCurrent` marks the one in force now. It also seeds the time zone offered to teams beneath it (Teams_GetOperatingModelDefaults). Methodology, sizing and working weeks belong to teams.")]
    [McpTool("TeamsOfTeams_GetOperatingModels", "List team of teams operating models")]
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
    [EndpointDescription("Takes the UUID `id`. The time zone is the one its parent team of teams had in effect on the start date (yyyy-MM-dd), else the system default.")]
    [McpTool("TeamsOfTeams_GetOperatingModelDefaults", "Get team of teams operating model defaults")]
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
    [EndpointSummary("Get one of a team of teams' operating models.")]
    [EndpointDescription("Takes the UUID `id` and the model's UUID `operatingModelId` from TeamsOfTeams_GetOperatingModels.")]
    [McpTool("TeamsOfTeams_GetOperatingModel", "Get team of teams operating model")]
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
    [EndpointSummary("Change a team of teams' time zone from a date, by starting a new operating model.")]
    [EndpointDescription(
        "Takes the UUID `id`. The new model starts on `startDate` (yyyy-MM-dd), which must be after the current model's start; the current model is closed the day before. `timeZone` is an IANA id. To fix the time zone for a model's whole period instead, use TeamsOfTeams_UpdateOperatingModel. Returns the new model's id.")]
    [McpTool("TeamsOfTeams_SetOperatingModel", "Set team of teams operating model")]
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
    [EndpointDescription(
        "Takes the UUID `id` and the model's `operatingModelId`; any model, current or past, can be corrected, and its dates do not change. Sets its IANA `timeZone`, the model's only value, for every date it covers.")]
    [McpTool("TeamsOfTeams_UpdateOperatingModel", "Correct team of teams operating model")]
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
    [EndpointDescription(
        "Takes the UUID `id` and the current model's `operatingModelId`. Only the current model can be deleted, and never the only one; the previous model becomes current again with no end date.")]
    [McpTool("TeamsOfTeams_DeleteOperatingModel", "Delete team of teams operating model")]
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
    [EndpointSummary("Get a team of teams' own risks.")]
    [EndpointDescription(
        "Takes the UUID `id`. Only risks raised on the team of teams itself, not those of the teams beneath it (Teams_GetRisks per team, or Risks_GetList for all). Open risks only unless `includeClosed` is true. Each has its `id`, `key`, summary, status, ROAM category, `exposure` (Low, Medium or High), assignee and follow-up date. Returns 404 for an unknown team of teams.")]
    [McpTool("TeamsOfTeams_GetRisks", "List team of teams risks")]
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
    [EndpointSummary("Get one of a team of teams' risks.")]
    [EndpointDescription(
        "Takes the team of teams' UUID `id` and the risk's UUID or integer key in `riskIdOrKey`. Returns 404 when the risk belongs to another team. Status, category, impact, likelihood and exposure each come as `{ id, name }`; the `id` is what TeamsOfTeams_UpdateRisk takes.")]
    [McpTool("TeamsOfTeams_GetRisk", "Get team of teams risk")]
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
    [EndpointDescription(
        "Takes the team of teams' UUID `id`, which must match the body's `teamId`. `categoryId` is the ROAM category's numeric id from Risks_GetCategories (1 Resolved, 2 Owned, 3 Accepted, 4 Mitigated); `impactId` and `likelihoodId` are grade ids from Risks_GetGrades (1 Low, 2 Medium, 3 High). `assigneeId` is an employee UUID. The risk starts Open, reported now by the caller, whose account must be linked to an employee.")]
    [McpTool("TeamsOfTeams_CreateRisk", "Create team of teams risk", Destructive = false)]
    [ProducesResponseType(typeof(ObjectIdAndKey), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ObjectIdAndKey>> CreateRisk(Guid id, [FromBody] CreateRiskRequest request, CancellationToken cancellationToken)
    {
        if (id != request.TeamId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.TeamId), HttpContext));

        var teamExists = await _dispatcher.Send(new TeamOfTeamsExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var result = await _dispatcher.Send(request.ToCreateRiskCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRiskById), new { id, riskIdOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/risks/{riskId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Update one of a team of teams' risks, including closing or reopening it.")]
    [EndpointDescription(
        "**This is a whole-record overwrite: an omitted description, assignee, follow-up date or response is cleared.** Read the risk first with TeamsOfTeams_GetRisk and pass back what should stay. Takes the team of teams' UUID `id` and the risk's UUID `riskId`, which must match the body's `teamId` and `riskId`; the risk cannot be moved to another team. `statusId` is 1 Open or 2 Closed (Risks_GetStatuses); closing stamps the closed date and reopening clears it. `categoryId`, `impactId` and `likelihoodId` are as for TeamsOfTeams_CreateRisk.")]
    [McpTool("TeamsOfTeams_UpdateRisk", "Update team of teams risk")]
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
    [EndpointDescription(
        "Takes the UUID `id`. Only people who are members of the team of teams itself, not the members of the teams beneath it. One entry per person, by name, with the `employee` (`id` is the employee UUID the member tools take) and every team member `role` they hold. An unknown team of teams returns an empty list.")]
    [McpTool("TeamsOfTeams_GetMembers", "List team of teams members")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamMemberDto>>> GetMembers(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _dispatcher.Send(new TeamsMemberQueries.GetTeamMembersQuery(id), cancellationToken));
    }

    [HttpPost("{id}/members")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Add a member to a team of teams.")]
    [EndpointDescription(
        "Takes the UUID `id`, the `employeeId` (employee UUID) and at least one `roleIds` entry: team member role UUIDs from TeamMemberRoles_GetList. The team of teams and the employee must be active. Adding someone who is already a member adds the roles; a role they already hold here is refused.")]
    [McpTool("TeamsOfTeams_AddMember", "Add team of teams member", Destructive = false)]
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
    [EndpointDescription(
        "Takes the UUID `id` and the member's `employeeId`. **`roleIds` replaces the member's roles here**: roles left out are removed, so pass every role they should keep (at least one; to remove them, use TeamsOfTeams_RemoveMember). Read the current roles with TeamsOfTeams_GetMembers first.")]
    [McpTool("TeamsOfTeams_UpdateMember", "Change team of teams member roles")]
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
    [EndpointDescription(
        "Takes the UUID `id` and the member's `employeeId`, and removes every role they hold here. Their allocations are not changed; end those with Employees_ChangeAllocations.")]
    [McpTool("TeamsOfTeams_RemoveMember", "Remove team of teams member")]
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
