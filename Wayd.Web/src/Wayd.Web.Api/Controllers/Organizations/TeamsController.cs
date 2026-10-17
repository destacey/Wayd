using Wayd.Organization.Application.EmployeeCapacities;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;
using CsvHelper;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Organization.Application.Models;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Teams.Imports;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.Iterations.Queries;
using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Planning.Application.Risks.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Organizations;
using Wayd.Web.Api.Models.Organizations.Teams;
using Wayd.Web.Api.Models.Planning.Risks;
using Wayd.Work.Application.WorkItemDependencies.Dtos;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.Application.WorkItems.Forecasting;
using Wayd.Work.Application.WorkTeams.Dtos;
using Wayd.Work.Application.WorkTeams.Queries;

namespace Wayd.Web.Api.Controllers.Organizations;

[Route("api/organization/teams")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Teams)]
public class TeamsController(
    ILogger<TeamsController> logger,
    IDispatcher dispatcher,
    ICsvService csvService) : ControllerBase
{
    private readonly ILogger<TeamsController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICsvService _csvService = csvService;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get a list of teams.")]
    [McpTool("Teams_GetTeams", "List teams")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<TeamListDto>>> GetList(CancellationToken cancellationToken, bool includeInactive = false)
    {
        var teams = await _dispatcher.Send(new GetTeamsQuery(includeInactive), cancellationToken);
        return Ok(teams.OrderBy(e => e.Name));
    }

    [HttpGet("{id}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get team details.")]
    [McpTool("Teams_GetTeam", "Get team")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDetailsDto>> GetById(int id)
    {
        var team = await _dispatcher.Send(new GetTeamQuery(id));

        return team is not null
            ? Ok(team)
            : NotFound();
    }

    [HttpGet("{idOrKey}/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary(
        "Get a team's activity history, newest first: the team's creation, detail changes, activation and deactivation, members joining, leaving or changing roles (by employee and role id), its membership in a team of teams being added, re-dated or removed, and operating models being set, corrected or removed.")]
    [EndpointDescription(
        "Each entry has a `category` (Created, Updated, ScheduleChanged, StatusChanged, StateChanged, Health, Removed, Baseline), an `actorKind` (User, System, Import, Sync, Anonymous) with the acting `employee` when there is one, a `timestamp`, a one-line `summary`, and a `payload`: the event's fields as a JSON string. A change carries both ends, the value before and after. People in a payload are employee ids, not user ids. A Baseline entry marks where tracking began for a record that already existed, holding what it looked like then; nothing before it was recorded. An entry with `isRelated: true` was raised on another record and is listed here because it concerns this one; `raisedOn` names that record, or is null where it could not be resolved (typically removed since). Paged: the response carries `totalCount` and `hasNextPage`.")]
    [McpTool("Teams_GetActivities", "Get team activity history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(new GetTeamActivitiesQuery(new IdOrKey(idOrKey), page, pageSize), cancellationToken);

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Teams)]
    [EndpointSummary("Create a team.")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateTeamCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Key }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.Teams)]
    [EndpointSummary("Submit a csv file of teams to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(TeamImportDefinition.ImportKey)]
    public async Task<ActionResult> Import([FromForm, CsvRows(typeof(ImportTeamRequest))] IFormFile file, [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly, [FromServices] ImportSubmissionResponder responder, CancellationToken cancellationToken)
    {
        try
        {
            var importedTeams = _csvService.ReadCsv<ImportTeamRequest>(file.OpenReadStream());

            List<SubmittedImportRow<ImportTeamDto>> rows = [];
            var validator = new ImportTeamRequestValidator();
            foreach (var team in importedTeams)
            {
                var validationResults = await validator.ValidateAsync(team, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Team Code: {team.Code})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                rows.Add(new SubmittedImportRow<ImportTeamDto>(team.ImportId, team.ToImportTeamDto()));
            }

            var result = await _dispatcher.Send(new ImportTeamsCommand(rows, submissionGroupId, validateOnly), cancellationToken);

            return result.IsSuccess
                ? await responder.Respond(this, result.Value, cancellationToken)
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.Message, HttpContext));
        }
    }

    [HttpPost("members/import")]
    [MustHavePermission(ApplicationAction.ManageTeamMemberships, ApplicationResource.Teams)]
    [EndpointSummary("Submit a csv file of team staffing rows to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(TeamMemberImportDefinition.ImportKey)]
    public async Task<ActionResult> ImportMembers([FromForm, CsvRows(typeof(ImportTeamMemberRequest))] IFormFile file, [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly, [FromServices] ImportSubmissionResponder responder, CancellationToken cancellationToken)
    {
        try
        {
            var importedMembers = _csvService.ReadCsv<ImportTeamMemberRequest>(file.OpenReadStream());

            List<SubmittedImportRow<ImportTeamMemberDto>> rows = [];
            var validator = new ImportTeamMemberRequestValidator();
            foreach (var member in importedMembers)
            {
                var validationResults = await validator.ValidateAsync(member, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Team Code: {member.TeamCode}, Employee Number: {member.EmployeeNumber})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                rows.Add(new SubmittedImportRow<ImportTeamMemberDto>(
                    member.ImportId, member.ToImportTeamMemberDto()));
            }

            var result = await _dispatcher.Send(new ImportTeamMembersCommand(rows, submissionGroupId, validateOnly), cancellationToken);

            return result.IsSuccess
                ? await responder.Respond(this, result.Value, cancellationToken)
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.Message, HttpContext));
        }
    }

    [HttpPost("team-memberships/import")]
    [MustHavePermission(ApplicationAction.ManageTeamMemberships, ApplicationResource.Teams)]
    [EndpointSummary("Import the team hierarchy (parent/child team memberships) from a csv file. Returns the run — 200 once it has finished, 202 while it is still queued or running.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(TeamMembershipImportDefinition.ImportKey)]
    public async Task<ActionResult> ImportTeamMemberships([FromForm, CsvRows(typeof(ImportTeamMembershipRequest))] IFormFile file, [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly, [FromServices] ImportSubmissionResponder responder, CancellationToken cancellationToken)
    {
        try
        {
            var importedMemberships = _csvService.ReadCsv<ImportTeamMembershipRequest>(file.OpenReadStream());

            List<SubmittedImportRow<ImportTeamMembershipDto>> rows = [];
            var validator = new ImportTeamMembershipRequestValidator();
            foreach (var membership in importedMemberships)
            {
                var validationResults = await validator.ValidateAsync(membership, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Child: {membership.ChildCode}, Parent: {membership.ParentCode})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                rows.Add(new SubmittedImportRow<ImportTeamMembershipDto>(
                    membership.ImportId, membership.ToImportTeamMembershipDto()));
            }

            var result = await _dispatcher.Send(new ImportTeamMembershipsCommand(rows, submissionGroupId, validateOnly), cancellationToken);

            return result.IsSuccess
                ? await responder.Respond(this, result.Value, cancellationToken)
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.Message, HttpContext));
        }
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Update a team.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateTeamCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/deactivate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Deactivate a team.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Deactivate(Guid id, [FromBody] DeactivateTeamRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToDeactivateTeamCommand(), cancellationToken);

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
    public async Task<ActionResult<IEnumerable<TeamMembershipDto>>> GetTeamMemberships(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _dispatcher.Send(new GetTeamMembershipsQuery(id), cancellationToken));
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
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.TeamId), HttpContext));

        var result = await _dispatcher.Send(request.ToTeamAddParentTeamMembershipCommand(), cancellationToken);

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

        var result = await _dispatcher.Send(request.ToTeamUpdateTeamMembershipCommand(), cancellationToken);

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

    #region Work Items

    // TODO: update the claims check for viewing teams and work items
    [HttpGet("{idOrCode}/backlog")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [EndpointSummary("Get the backlog for a team.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<WorkItemBacklogItemDto>>> GetTeamBacklog(string idOrCode, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetTeamBacklogQuery(idOrCode), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{idOrCode}/backlog-health")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [EndpointSummary("Grade a team's open backlog.")]
    [EndpointDescription(
        "Returns `checks` — Runway, Net Flow and WIP Load measure the whole backlog; Stale, Old Proposed, Aging WIP, Missing Estimate, Oversized, No Parent, No Project, Unassigned Active, Carry-over, Closed Parent and Rank Inversion flag work items — each with an `outcome` (Assessed, Not Enough History, Not Applicable), a `grade` (Healthy, At Risk, Unhealthy; absent when not assessed), and a `value` (weeks, a ratio, active items per member, or the percent of in-scope work items flagged). `workItems` lists every open backlog work item in rank order with the `flags` that apply to it, and `thresholds` states the values it was graded with. Every threshold is optional and falls back to its default. Estimates (`totalEstimate`, `oversizedEstimate`, each work item's `estimate`) are in the team's current `sizingMethod` (StoryPoints, Effort or Size); a work item with no value in it is missing an estimate, and 0 is an estimate. For a team that sizes by Count, Missing Estimate and Oversized are Not Applicable.")]
    [McpTool("Teams_GetBacklogHealth", "Grade team backlog health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TeamBacklogHealthDto>> GetTeamBacklogHealth(
        string idOrCode,
        [FromQuery] GetTeamBacklogHealthRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToGetTeamBacklogHealthQuery(idOrCode), cancellationToken);

        return result.IsFailure
            ? BadRequest(result.ToBadRequestObject(HttpContext))
            : result.Value is not null
                ? Ok(result.Value)
                : NotFound();
    }

    [HttpGet("{idOrCode}/work-distribution")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [EndpointSummary("Report where a team's completed work went.")]
    [EndpointDescription(
        "Groups the Requirement-tier work items the team completed between `from` and `to` (yyyy-MM-dd, inclusive, UTC, max 366 days) by portfolio, program, project, strategic theme or work type. Measures: Count, or StoryPoints (only work done while the team sized in story points; unestimated items excluded or filled from the team average). Work with no project is its own group.")]
    [McpTool("Teams_GetTeamWorkDistribution", "Report team work distribution")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TeamWorkDistributionDto>> GetTeamWorkDistribution(
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
    [EndpointSummary("Get the capacity of a team over a date range, from the time people are allocated to it.")]
    [EndpointDescription(
        "Returns, for `from` to `to` (yyyy-MM-dd, inclusive, max 731 days), the `workingDays` in the range and each person allocated to the team with their person-days: `allocatedPersonDays` in every role, `deliveryPersonDays` in roles that count toward delivery capacity, and `elsewherePersonDays` allocated to other teams on the same days. One person-day is one person allocated in full for one working day. `roles` breaks the allocated time down by team member role, each line saying whether it counts toward delivery capacity, and adds up to `allocatedPersonDays`. Each person carries their `allocations` to this team with the `personDays` each is worth, and their `elsewhereAllocations` to other teams in the range. A working day is a day in the team's working week that is not one of its holidays. `canManage` says whether the caller may change the allocations. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamCapacityDto>> GetCapacity(Guid id, [FromQuery] string? from, [FromQuery] string? to, CancellationToken cancellationToken)
    {
        if (!IsoDateQuery.TryParse(from, out var firstDay) || !IsoDateQuery.TryParse(to, out var lastDay))
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(IsoDateQuery.FormatError, HttpContext));

        if (firstDay is null || lastDay is null)
            return BadRequest(ProblemDetailsExtensions.ForBadRequest("Both from and to dates are required.", HttpContext));

        var capacity = await _dispatcher.Send(new GetTeamCapacityQuery(id, TeamType.Team, firstDay.Value, lastDay.Value), cancellationToken);

        return capacity is not null
            ? Ok(capacity)
            : NotFound();
    }

    [HttpGet("{id:guid}/capacity/report")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Report the capacity of a team over a date range.")]
    [EndpointDescription(
        "Returns, for `from` to `to` (yyyy-MM-dd, inclusive, max 731 days), one `lines` entry per person and team member role allocated to the team. A line's `total` is in person-days: `allocatedPersonDays` over the team's working days, the part lost to `holidayPersonDays` and `timeAwayPersonDays`, what is left as `availablePersonDays`, and the part of that in roles that count toward delivery capacity as `deliveryPersonDays`. `interval` (`Week` or `Month`) also splits each line into `buckets`, in the order of the report's `buckets`. `teams` holds the team alone, as `nodeId` 0. `people` gives each person's time over the system default working days that the report does not count: `outsidePersonDays` allocated to other teams and `unallocatedPersonDays` allocated nowhere. Time away is reported as time lost, never why. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamCapacityReportDto>> GetCapacityReport(Guid id, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] CapacityReportInterval? interval, CancellationToken cancellationToken)
    {
        if (!IsoDateQuery.TryParse(from, out var firstDay) || !IsoDateQuery.TryParse(to, out var lastDay))
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(IsoDateQuery.FormatError, HttpContext));

        if (firstDay is null || lastDay is null)
            return BadRequest(ProblemDetailsExtensions.ForBadRequest("Both from and to dates are required.", HttpContext));

        var report = await _dispatcher.Send(new GetTeamCapacityReportQuery(id, TeamType.Team, firstDay.Value, lastDay.Value, interval), cancellationToken);

        return report is not null
            ? Ok(report)
            : NotFound();
    }

    [HttpGet("{id:guid}/capacity/plan")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get the capacity of a team over a date range day by day, to plan against.")]
    [EndpointDescription(
        "Returns, for `from` to `to` (yyyy-MM-dd, inclusive, max 366 days), the `capacity` the `capacity` endpoint returns for the same range, the `days` of the team's working week in the range, and for each person allocated to it their time day by day. Each person has a `rows` entry per team member role with the `days` that role has time allocated: the `percent` and the `personDays` it is worth after the holidays the person takes and their time away. Their own `days` list the working days on which something else decides their time: `isHoliday` in the calendar they take, `percentAway` (never why), and `elsewherePercent` allocated to other teams. A day left out of a list has none of it. `daysOff` (repeatable) are days the whole team is off beyond its working week and holidays, such as a sprint's team days off: yyyy-MM-dd for a full day, or yyyy-MM-dd:percent for part of one (`2026-10-01:50` is half the day). Each takes its share of everyone's time that day and is listed in `teamDaysOff`; a day keeps its percent, worth only the share the team works, so 0 person-days on a full day off. Nothing is stored per day: change a day or a week through the change allocations endpoint, as a change over that range. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamCapacityPlanDto>> GetCapacityPlan(Guid id, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] List<string>? daysOff, CancellationToken cancellationToken)
    {
        if (!IsoDateQuery.TryParse(from, out var firstDay) || !IsoDateQuery.TryParse(to, out var lastDay))
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(IsoDateQuery.FormatError, HttpContext));

        if (firstDay is null || lastDay is null)
            return BadRequest(ProblemDetailsExtensions.ForBadRequest("Both from and to dates are required.", HttpContext));

        List<TeamDayOff> teamDaysOff = [];
        foreach (var day in daysOff ?? [])
        {
            if (!TeamDayOff.TryParse(day, out var dayOff))
                return BadRequest(ProblemDetailsExtensions.ForBadRequest("Each day off must be yyyy-MM-dd, optionally followed by :percent off from 1 to 100.", HttpContext));
            teamDaysOff.Add(dayOff);
        }

        var plan = await _dispatcher.Send(new GetTeamCapacityPlanQuery(id, TeamType.Team, firstDay.Value, lastDay.Value, teamDaysOff), cancellationToken);

        return plan is not null
            ? Ok(plan)
            : NotFound();
    }

    [HttpGet("{id:guid}/capacity/proposal")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Propose allocations for a team from its members, without saving anything.")]
    [EndpointDescription(
        "Returns one row per active member and active role they hold on the team, for allocations starting on `from` (yyyy-MM-dd). A row has no `end` unless the member already has a later allocation to it, in which case it ends the day before so that allocation is untouched; a member with time on it on `from` has no rows. `remainingPercent` is what is left of the member's time across all teams: 100 less their most allocated day from `from` to `end`. `percent` is that remainder split evenly between the member's roles. A member with nothing left is returned at 0, not left out. Nothing is saved: send the rows to keep to the change allocations endpoint. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamAllocationProposalDto>> GetAllocationProposal(Guid id, [FromQuery] string? from, CancellationToken cancellationToken)
    {
        if (!IsoDateQuery.TryParse(from, out var firstDay))
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(IsoDateQuery.FormatError, HttpContext));

        if (firstDay is null)
            return BadRequest(ProblemDetailsExtensions.ForBadRequest("The from date is required.", HttpContext));

        var proposal = await _dispatcher.Send(new GetTeamAllocationProposalQuery(id, TeamType.Team, firstDay.Value), cancellationToken);

        return proposal is not null
            ? Ok(proposal)
            : NotFound();
    }

    [HttpGet("{idOrCode}/throughput-forecast")]
    [FeatureGate(FeatureFlags.Names.DeliveryForecasting)]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [EndpointSummary(
        "Forecast how many of a team's open backlog work items it will finish from today through `targetDate`, by Monte Carlo simulation of its recent daily throughput.")]
    [EndpointDescription(
        "Returns an `outcome` (Forecast, or Not Enough History when the team finished fewer than 10 backlog work items in the window), `backlogWorkItems` open today, `percentiles` — at each `confidence` the `workItems` count finished at least that often and `throughWorkItem`, the backlog work item that count reaches (active items first unless `startedWorkFirst` is false, then rank order) — and a `histogram` of trials by work items finished. Requires the delivery-forecasting feature flag; returns 404 when it is off.")]
    [McpTool("Teams_GetThroughputForecast", "Forecast team throughput")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamThroughputForecastDto>> GetTeamThroughputForecast(
        string idOrCode,
        [FromQuery] string targetDate,
        [FromQuery] int? lookbackDays,
        [FromQuery] bool? startedWorkFirst,
        CancellationToken cancellationToken)
    {
        if (!IsoDateQuery.TryParse(targetDate, out var parsedTargetDate) || parsedTargetDate is null)
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(IsoDateQuery.FormatError, HttpContext));

        var query = new GetTeamThroughputForecastQuery(
            idOrCode,
            parsedTargetDate.Value,
            lookbackDays ?? ForecastOptions.DefaultLookbackDays,
            startedWorkFirst ?? true);
        var result = await _dispatcher.Send(query, cancellationToken);

        return result.IsFailure
            ? BadRequest(result.ToBadRequestObject(HttpContext))
            : result.Value is not null
                ? Ok(result.Value)
                : NotFound();
    }

    [HttpGet("{idOrCode}/work-items")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [EndpointSummary("Get the work items for a team.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<WorkItemListDto>>> GetTeamWorkItems(
        string idOrCode,
        [FromQuery] WorkStatusCategory[]? statusCategories,
        [FromQuery] DateTime? doneFrom,
        [FromQuery] DateTime? doneTo,
        CancellationToken cancellationToken)
    {
        Instant? doneFromInstant = null;
        Instant? doneToInstant = null;

        if (doneFrom.HasValue)
        {
            var df = doneFrom.Value;
            df = df.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(df, DateTimeKind.Utc) : df.ToUniversalTime();
            doneFromInstant = Instant.FromDateTimeUtc(df);
        }

        if (doneTo.HasValue)
        {
            var dt = doneTo.Value;
            dt = dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();
            doneToInstant = Instant.FromDateTimeUtc(dt);
        }

        var workItems = await _dispatcher.Send(new GetTeamWorkItemsQuery(idOrCode, statusCategories, doneFromInstant, doneToInstant), cancellationToken);

        return workItems is null
            ? NotFound()
            : Ok(workItems);
    }

    [HttpGet("{id}/dependencies")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [EndpointSummary("Get the active dependencies for a team.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<DependencyDto>>> GetTeamDependencies(Guid id, CancellationToken cancellationToken)
    {
        var dependencies = await _dispatcher.Send(new GetTeamDependenciesQuery(id, [DependencyState.ToDo, DependencyState.InProgress]), cancellationToken);

        return dependencies is null
            ? NotFound()
            : Ok(dependencies);
    }

    #endregion Work Items

    #region Risks

    [HttpGet("{id}/risks")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get team risks.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<RiskListDto>>> GetRisks(Guid id, CancellationToken cancellationToken, bool includeClosed = false)
    {
        var teamExists = await _dispatcher.Send(new TeamExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var risks = await _dispatcher.Send(new GetRisksQuery(id, includeClosed), cancellationToken);

        return Ok(risks);
    }

    [HttpGet("{id}/risks/{riskIdOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get a team risk by Id.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RiskDetailsDto>> GetRiskById(Guid id, string riskIdOrKey, CancellationToken cancellationToken)
    {
        var teamExists = await _dispatcher.Send(new TeamExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var risk = await _dispatcher.Send(new GetRiskQuery(riskIdOrKey), cancellationToken);

        return risk is not null && risk.Team?.Id == id
            ? Ok(risk)
            : NotFound();
    }

    [HttpPost("{id}/risks")]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Risks)]
    [EndpointSummary("Create a risk for a team.")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> CreateRisk(Guid id, [FromBody] CreateRiskRequest request, CancellationToken cancellationToken)
    {
        if (id != request.TeamId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.TeamId), HttpContext));

        var teamExists = await _dispatcher.Send(new TeamExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var result = await _dispatcher.Send(request.ToCreateRiskCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRiskById), new { id, riskIdOrKey = result.Value.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/risks/{riskId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Risks)]
    [EndpointSummary("Update a team risk.")]
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

    #region Operating Models

    [HttpGet("{id}/operating-models/{operatingModelId}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get a specific operating model for a team.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamOperatingModelDetailsDto>> GetOperatingModel(Guid id, Guid operatingModelId, CancellationToken cancellationToken)
    {
        var teamExists = await _dispatcher.Send(new TeamExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var operatingModel = await _dispatcher.Send(new GetTeamOperatingModelQuery(id, operatingModelId), cancellationToken);

        return operatingModel is not null
            ? Ok(operatingModel)
            : NotFound();
    }

    [HttpGet("{id}/operating-models")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get the operating model history for a team.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<TeamOperatingModelDetailsDto>>> GetOperatingModels(Guid id, CancellationToken cancellationToken)
    {
        var history = await _dispatcher.Send(new GetTeamOperatingModelsQuery(id), cancellationToken);

        return history is not null
            ? Ok(history)
            : NotFound();
    }

    [HttpGet("{id}/operating-models/as-of")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get the current operating model for a team, or the model effective on a specific date.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamOperatingModelDetailsDto>> GetOperatingModelAsOf(Guid id, [FromQuery] DateTime? asOfDate, CancellationToken cancellationToken)
    {
        // TODO: using LocalDate or DateOnly from NSWAG and axios doesn't working correctly.
        // NSwag's TypeScript generator uses toISOString() for all Date types, regardless of whether the OpenAPI spec specifies format: date or format: date-time. This is a known NSwag limitation (Issue #2339).
        LocalDate? localDate = asOfDate.HasValue
            ? LocalDate.FromDateTime(asOfDate.Value)
            : null;

        var operatingModel = await _dispatcher.Send(new GetTeamOperatingModelAsOfQuery(id, localDate), cancellationToken);

        return operatingModel is not null
            ? Ok(operatingModel)
            : NotFound();
    }

    [HttpGet("operating-models")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get operating models for multiple teams.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<TeamOperatingModelDetailsDto>>> GetOperatingModelsForTeams(
        [FromQuery] Guid[] teamIds,
        [FromQuery] DateTime? asOfDate,
        CancellationToken cancellationToken)
    {
        // TODO: using LocalDate or DateOnly from NSWAG and axios doesn't working correctly.
        // NSwag's TypeScript generator uses toISOString() for all Date types, regardless of whether the OpenAPI spec specifies format: date or format: date-time. This is a known NSwag limitation (Issue #2339).
        LocalDate? localDate = asOfDate.HasValue
            ? LocalDate.FromDateTime(asOfDate.Value)
            : null;

        var operatingModels = await _dispatcher.Send(new GetTeamOperatingModelsForTeamsQuery(teamIds, localDate), cancellationToken);

        return Ok(operatingModels);
    }

    [HttpGet("{id}/operating-models/defaults")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get the values a new operating model for a team is pre-filled with.")]
    [EndpointDescription("The time zone is the one the team's parent team of teams had in effect on the start date (yyyy-MM-dd), else the system default; the commitment grace period is the system default.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperatingModelDefaultsDto>> GetOperatingModelDefaults(Guid id, [FromQuery] string startDate, CancellationToken cancellationToken)
    {
        if (!IsoDateQuery.TryParse(startDate, out var parsedStartDate) || parsedStartDate is null)
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(IsoDateQuery.FormatError, HttpContext));

        var teamExists = await _dispatcher.Send(new TeamExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var defaults = await _dispatcher.Send(new GetOperatingModelDefaultsQuery(id, parsedStartDate.Value), cancellationToken);

        return defaults is not null
            ? Ok(defaults)
            : NotFound();
    }

    [HttpGet("{id}/has-ever-been-scrum")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Check if a team has ever used the Scrum methodology.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<bool>> HasEverBeenScrum(Guid id, CancellationToken cancellationToken)
    {
        var hasBeenScrum = await _dispatcher.Send(new TeamHasEverBeenScrumQuery(id), cancellationToken);

        return hasBeenScrum is not null
            ? Ok(hasBeenScrum)
            : NotFound();
    }

    [HttpPost("{id}/operating-models")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Set a new operating model for a team.")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> SetOperatingModel(Guid id, [FromBody] SetTeamOperatingModelRequest request, CancellationToken cancellationToken)
    {
        var teamExists = await _dispatcher.Send(new TeamExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var result = await _dispatcher.Send(request.ToSetTeamOperatingModelCommand(id), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetOperatingModel), new { id, operatingModelId = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/operating-models/{operatingModelId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Update an existing operating model for a team.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateOperatingModel(Guid id, Guid operatingModelId, [FromBody] UpdateTeamOperatingModelRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdateTeamOperatingModelCommand(id, operatingModelId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/operating-models/{operatingModelId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Delete an operating model from a team.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteOperatingModel(Guid id, Guid operatingModelId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteTeamOperatingModelCommand(id, operatingModelId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Operating Models

    [HttpGet("{id}/sprints")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Iterations)]
    [EndpointSummary("Get the sprints for a team.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<SprintListDto>>> GetSprints(Guid id, CancellationToken cancellationToken)
    {
        var sprints = await _dispatcher.Send(new GetSprintsQuery(id), cancellationToken);

        return Ok(sprints);
    }

    [HttpGet("{id}/sprints/active")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Iterations)]
    [EndpointSummary("Get the team's active sprint")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SprintDetailsDto>> GetActiveSprint(Guid id, CancellationToken cancellationToken)
    {
        var sprint = await _dispatcher.Send(new GetTeamActiveSprintQuery(id), cancellationToken);

        return Ok(sprint);
    }

    [HttpGet("{id}/sprint-metrics")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Iterations)]
    [EndpointSummary("Get the team's sprint metrics.")]
    [EndpointDescription("The team's velocity, predictability and say/do over its last completed standard sprints: each sprint's values, their average and their spread, by item count and in the team's sizing method. Non-standard sprints, sprints whose work item history is incomplete and sprints with nothing committed are left out and listed with the reason. sprintWindow is how many sprints to measure over (3 to 12); omit it for the organization's sprint window. Estimates are in the team's sizing method now; a sprint measured in an earlier one counts by item only. Returns 404 for an id that is not a team's, including a team of teams, which has no sprints of its own.")]
    [McpTool("Teams_GetSprintMetrics", "Get team sprint metrics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TeamSprintMetricsDto>> GetSprintMetrics(Guid id, [FromQuery] int? sprintWindow, CancellationToken cancellationToken)
    {
        var metrics = await _dispatcher.Send(new GetTeamSprintMetricsQuery(id, sprintWindow), cancellationToken);

        return metrics is not null
            ? Ok(metrics)
            : NotFound();
    }

    [HttpGet("{id}/cadence-metrics")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [EndpointSummary("Get the team's cadence velocity.")]
    [EndpointDescription("A Kanban team's velocity over its reporting cadence: the Requirement-tier work it completed (Done-category status, not Removed) in each recent period, by item count and in the sizing method of the period's operating model, with the average and spread across ended, whole periods. Periods follow each operating model's cadence in that model's time zone; a period cut short where an operating model changes the cadence, time zone or sizing method inside it is partial and left out of the averages, as is the current period. periodWindow is how many ended periods to measure over (3 to 12); omit it for the organization's sprint window. There is no commitment, so no predictability or say/do. A team with no cadence returns no periods. Returns 404 for an id that is not a team's, including a team of teams, which has no cadence.")]
    [McpTool("Teams_GetCadenceMetrics", "Get team cadence velocity")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TeamCadenceMetricsDto>> GetCadenceMetrics(Guid id, [FromQuery] int? periodWindow, CancellationToken cancellationToken)
    {
        var metrics = await _dispatcher.Send(new GetTeamCadenceMetricsQuery(id, periodWindow), cancellationToken);

        return metrics is not null
            ? Ok(metrics)
            : NotFound();
    }

    [HttpGet("functional-organization-chart")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get the functional organizaation chart for a given date.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<FunctionalOrganizationChartDto>> GetFunctionalOrganizationChart([FromQuery] DateTime? asOfDate, CancellationToken cancellationToken)
    {
        // TODO: using LocalDate or DateOnly from NSWAG and axios doesn't working correctly.
        // NSwag's TypeScript generator uses toISOString() for all Date types, regardless of whether the OpenAPI spec specifies format: date or format: date-time. This is a known NSwag limitation (Issue #2339).
        LocalDate? dateOnlyAsOfDate = asOfDate.HasValue
            ? LocalDate.FromDateTime(asOfDate.Value)
            : null;
        var orgChart = await _dispatcher.Send(new GetFunctionalOrganizationChartQuery(dateOnlyAsOfDate), cancellationToken);

        return Ok(orgChart);
    }

    #region Team Members

    [HttpGet("{id}/members")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Teams)]
    [EndpointSummary("Get the members of a team.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamMemberDto>>> GetMembers(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _dispatcher.Send(new GetTeamMembersQuery(id), cancellationToken));
    }

    [HttpPost("{id}/members")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Add a member to a team.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AddMember(Guid id, [FromBody] AddTeamMemberRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new AddTeamMemberCommand(id, request.EmployeeId, request.RoleIds), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/members/{employeeId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Update a team member's roles.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateMember(Guid id, Guid employeeId, [FromBody] UpdateTeamMemberRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new UpdateTeamMemberCommand(id, employeeId, request.RoleIds), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/members/{employeeId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Remove a member from a team.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RemoveMember(Guid id, Guid employeeId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemoveTeamMemberCommand(id, employeeId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Team Members
}
