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
[McpTools(McpToolset.Teams, "wayd-teams")]
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
    [EndpointDescription(
        "`name` and `code` are each unique across teams and teams of teams; `code` is 2 to 10 uppercase letters and digits. `activeDate` (yyyy-MM-dd) is the day the team starts. The team starts with a Kanban, Count-sized operating model in the system default time zone and working week; change it with Teams_SetOperatingModel. To place it under a team of teams, call Teams_AddTeamMembership; to staff it, Teams_AddMember. Returns the new team's `id` (UUID) and `key` (integer).")]
    [McpTool("Teams_Create", "Create team", Destructive = false)]
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
    [EndpointSummary("Update a team's name, code and description.")]
    [EndpointDescription(
        "**This is a whole-record overwrite of those three fields: an omitted description is cleared.** Read the team first with Teams_GetTeam and pass back what should stay. Takes the team's UUID `id`, which must match the body's `id`. `code` is 2 to 10 uppercase letters and digits. The active date, operating model, members and team-of-teams membership each have their own tools.")]
    [McpTool("Teams_Update", "Update team")]
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
    [EndpointDescription(
        "Takes the team's UUID `id`, which must match the body's `id`, and the `inactiveDate` (yyyy-MM-dd), which must be after the team's active date. Refused when the team is already inactive, or when its latest team-of-teams membership has no end date or ends after `inactiveDate`: end or remove that membership first with Teams_UpdateTeamMembership or Teams_RemoveTeamMembership. No tool reactivates a team, so confirm with the user before calling.")]
    [McpTool("Teams_Deactivate", "Deactivate team")]
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
    [EndpointSummary("Get a team's memberships in teams of teams, past, current and future.")]
    [EndpointDescription(
        "Takes the team's UUID `id`. Each membership has its own `id` (the `teamMembershipId` the update and remove tools take), the `child` team and `parent` team of teams, `start` and `end` (yyyy-MM-dd; no end is open-ended), and a `state` (Past, Active or Future) as of today. A team belongs to at most one team of teams on any day. Returns 404 for an unknown team.")]
    [McpTool("Teams_GetTeamMemberships", "List team's team-of-teams memberships")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<TeamMembershipDto>>> GetTeamMemberships(Guid id, CancellationToken cancellationToken)
    {
        var memberships = await _dispatcher.Send(new GetTeamMembershipsQuery(id), cancellationToken);

        return memberships is null
            ? NotFound()
            : Ok(memberships);
    }

    [HttpPost("{id}/team-memberships")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Place a team under a team of teams for a date range.")]
    [EndpointDescription(
        "Takes the team's UUID `id`, which must match the body's `teamId`, and the parent team of teams' UUID in `parentTeamId` (resolve it with TeamsOfTeams_GetList). `start` and optional `end` are yyyy-MM-dd; no `end` is open-ended. Both teams must be active, and the range must not overlap another of the team's memberships: to move a team, first end its current membership with Teams_UpdateTeamMembership, then add the new one starting the day after. The hierarchy drives team-of-teams rollups (capacity, work distribution) for those dates.")]
    [McpTool("Teams_AddTeamMembership", "Add team to a team of teams", Destructive = false)]
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
    [EndpointSummary("Change the dates of a team's membership in a team of teams.")]
    [EndpointDescription(
        "Takes the team's UUID `id` and the membership's `teamMembershipId` from Teams_GetTeamMemberships, each of which must match the body. **Both dates are overwritten: an omitted `end` makes the membership open-ended**, so pass the current `end` back to keep it. Both teams must be active, and the new range must not overlap the team's other memberships. The parent cannot be changed here: end this membership and add another.")]
    [McpTool("Teams_UpdateTeamMembership", "Change team membership dates")]
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
    [EndpointSummary("Remove a team's membership in a team of teams.")]
    [EndpointDescription(
        "Takes the team's UUID `id` and the membership's `teamMembershipId` from Teams_GetTeamMemberships. The membership is deleted for its whole range, so the team drops out of the parent's rollups for past dates too; to record that a team left, end the membership with Teams_UpdateTeamMembership instead. Both teams must be active.")]
    [McpTool("Teams_RemoveTeamMembership", "Remove team from a team of teams")]
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
    [EndpointSummary("Get a team's open backlog in rank order.")]
    [EndpointDescription(
        "Takes the team's UUID or `code`. Returns the team's Requirement-tier work items (such as user stories and bugs) in a Proposed or Active status category, sorted by stack rank, each with its 1-based `rank`, `parentRank`, workspace, type, status, parent, sprint, assignee, project and its Story Points, Effort and Size estimates. Portfolio-tier items (epics, features) are not listed. An unknown team returns an empty list. To grade the backlog, use Teams_GetBacklogHealth.")]
    [McpTool("Teams_GetBacklog", "Get team backlog")]
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
        "Takes the team's UUID `id`. Returns, for `from` to `to` (yyyy-MM-dd, inclusive, max 731 days), the `workingDays` in the range and each person allocated to the team with their person-days: `allocatedPersonDays` in every role, `deliveryPersonDays` in roles that count toward delivery capacity, and `elsewherePersonDays` allocated to other teams on the same days. One person-day is one person allocated in full for one working day. `roles` breaks the allocated time down by team member role, each line saying whether it counts toward delivery capacity, and adds up to `allocatedPersonDays`. Each person carries their `allocations` to this team with the `personDays` each is worth, and their `elsewhereAllocations` to other teams in the range. A working day is a day in the team's working week that is not one of its holidays. `canManage` says whether the caller may change the allocations, which is done per person with Employees_ChangeAllocations. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [McpTool("Teams_GetCapacity", "Get team capacity")]
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
        "Takes the team's UUID `id`. Returns, for `from` to `to` (yyyy-MM-dd, inclusive, max 731 days), one `lines` entry per person and team member role allocated to the team. A line's `total` is in person-days: `allocatedPersonDays` over the team's working days, the part lost to `holidayPersonDays` and `timeAwayPersonDays`, what is left as `availablePersonDays`, and the part of that in roles that count toward delivery capacity as `deliveryPersonDays`. `interval` (`Week` or `Month`) also splits each line into `buckets`, in the order of the report's `buckets`. `teams` holds the team alone, as `nodeId` 0. `people` gives each person's time over the system default working days that the report does not count: `outsidePersonDays` allocated to other teams and `unallocatedPersonDays` allocated nowhere. Time away is reported as time lost, never why. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [McpTool("Teams_GetCapacityReport", "Report team capacity")]
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
        "Takes the team's UUID `id`. Returns, for `from` to `to` (yyyy-MM-dd, inclusive, max 366 days), the `capacity` the `capacity` endpoint (Teams_GetCapacity) returns for the same range, the `days` of the team's working week in the range, and for each person allocated to it their time day by day. Each person has a `rows` entry per team member role with the `days` that role has time allocated: the `percent` and the `personDays` it is worth after the holidays the person takes and their time away. Their own `days` list the working days on which something else decides their time: `isHoliday` in the calendar they take, `percentAway` (never why), and `elsewherePercent` allocated to other teams. A day left out of a list has none of it. `daysOff` (repeatable) are days the whole team is off beyond its working week and holidays, such as a sprint's team days off: yyyy-MM-dd for a full day, or yyyy-MM-dd:percent for part of one (`2026-10-01:50` is half the day). Each takes its share of everyone's time that day and is listed in `teamDaysOff`; a day keeps its percent, worth only the share the team works, so 0 person-days on a full day off. Nothing is stored per day: change a day or a week through the change allocations endpoint (Employees_ChangeAllocations), as a change over that range. Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [McpTool("Teams_GetCapacityPlan", "Get team capacity plan")]
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
        "Takes the team's UUID `id`. Returns one row per active member and active role they hold on the team, for allocations starting on `from` (yyyy-MM-dd). A row has no `end` unless the member already has a later allocation to it, in which case it ends the day before so that allocation is untouched; a member with time on it on `from` has no rows. `remainingPercent` is what is left of the member's time across all teams: 100 less their most allocated day from `from` to `end`. `percent` is that remainder split evenly between the member's roles. A member with nothing left is returned at 0, not left out. Nothing is saved: send the rows to keep to the change allocations endpoint (Employees_ChangeAllocations). Requires the team-capacity-management feature flag; returns 404 when it is off.")]
    [McpTool("Teams_GetAllocationProposal", "Propose team allocations")]
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
    [EndpointSummary("Get a team's Requirement-tier work items, optionally filtered by status category and done date.")]
    [EndpointDescription(
        "Takes the team's UUID or `code`. Lists the team's Requirement-tier work items (such as user stories and bugs; never epics or features), ordered by stack rank. `statusCategories` (repeatable: Proposed, Active, Done, Removed) narrows them; omit it for all. `doneFrom` and `doneTo` are ISO 8601 date-times (UTC when no offset is given) bounding when an item reached Done, and leave out items that are not done. For the open backlog in rank order, use Teams_GetBacklog. An unknown team returns an empty list.")]
    [McpTool("Teams_GetWorkItems", "List team work items")]
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
    [EndpointSummary("Get the open dependencies for a team.")]
    [EndpointDescription(
        "Takes the team's UUID `id`. Lists the work item dependencies, not removed, where the team owns either end and the predecessor (`source`) is not yet done (state To Do or In Progress); `target` is the successor waiting on it. Each has a `state`, a `health` (Healthy, At Risk, Unhealthy) from the two items' status and planned sprints, and a `scope` (Intra-Team, Cross-Team, or Unknown when an end has no team). Returns 404 for an unknown team.")]
    [McpTool("Teams_GetDependencies", "List team dependencies")]
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
    [EndpointSummary("Get a team's risks.")]
    [EndpointDescription(
        "Takes the team's UUID `id`. Open risks only unless `includeClosed` is true. Each has its `id`, `key`, summary, team, status, ROAM category, `exposure` (Low, Medium or High, from impact and likelihood), assignee and follow-up date. Returns 404 for an unknown team; a team of teams' risks are under TeamsOfTeams_GetRisks.")]
    [McpTool("Teams_GetRisks", "List team risks")]
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
    [EndpointSummary("Get one of a team's risks.")]
    [EndpointDescription(
        "Takes the team's UUID `id` and the risk's UUID or integer key in `riskIdOrKey`. Returns 404 when the risk belongs to another team. Status, category, impact, likelihood and exposure each come as `{ id, name }`; the `id` is what Teams_UpdateRisk takes.")]
    [McpTool("Teams_GetRisk", "Get team risk")]
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
    [EndpointDescription(
        "Takes the team's UUID `id`, which must match the body's `teamId`. `categoryId` is the ROAM category's numeric id from Risks_GetCategories (1 Resolved, 2 Owned, 3 Accepted, 4 Mitigated); `impactId` and `likelihoodId` are grade ids from Risks_GetGrades (1 Low, 2 Medium, 3 High). `assigneeId` is an employee UUID. The risk starts Open, reported now by the caller, whose account must be linked to an employee. The same as Risks_Create.")]
    [McpTool("Teams_CreateRisk", "Create team risk", Destructive = false)]
    [ProducesResponseType(typeof(ObjectIdAndKey), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ObjectIdAndKey>> CreateRisk(Guid id, [FromBody] CreateRiskRequest request, CancellationToken cancellationToken)
    {
        if (id != request.TeamId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.TeamId), HttpContext));

        var teamExists = await _dispatcher.Send(new TeamExistsQuery(id), cancellationToken);
        if (!teamExists)
            return NotFound();

        var result = await _dispatcher.Send(request.ToCreateRiskCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRiskById), new { id, riskIdOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/risks/{riskId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Risks)]
    [EndpointSummary("Update one of a team's risks, including closing or reopening it.")]
    [EndpointDescription(
        "**This is a whole-record overwrite: an omitted description, assignee, follow-up date or response is cleared.** Read the risk first with Teams_GetRisk and pass back what should stay. Takes the team's UUID `id` and the risk's UUID `riskId`, which must match the body's `teamId` and `riskId`; the risk cannot be moved to another team. `statusId` is 1 Open or 2 Closed (Risks_GetStatuses); closing stamps the closed date and reopening clears it. `categoryId`, `impactId` and `likelihoodId` are as for Teams_CreateRisk. The same as Risks_Update.")]
    [McpTool("Teams_UpdateRisk", "Update team risk")]
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
    [EndpointSummary("Get one of a team's operating models.")]
    [EndpointDescription(
        "Takes the team's UUID `id` and the model's UUID `operatingModelId` from Teams_GetOperatingModels. An operating model is how the team works from its `start` to its `end` (none while current): its `methodology` (Scrum or Kanban), `sizingMethod` (StoryPoints, Count, Effort or Size), IANA `timeZone`, `commitmentGraceDays`, `workingDays`, `holidayCalendar` (null for the system default), and for Kanban an optional reporting cadence (`cadenceWeeks` and `cadenceAnchor`).")]
    [McpTool("Teams_GetOperatingModel", "Get team operating model")]
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
    [EndpointDescription(
        "Takes the team's UUID `id`. Every operating model the team has had, back to back by date; `isCurrent` marks the one in force now. See Teams_GetOperatingModel for the fields.")]
    [McpTool("Teams_GetOperatingModels", "List team operating models")]
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
    [EndpointDescription(
        "Takes the team's UUID `id`. `asOfDate` is an ISO 8601 date (its calendar date is used); omit it for today. Returns 404 when the team has no model in force on that date, such as a date before it started.")]
    [McpTool("Teams_GetOperatingModelAsOf", "Get team operating model on a date")]
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
    [EndpointSummary("Get the operating models in force for several teams on one date.")]
    [EndpointDescription(
        "`teamIds` (repeatable) are team UUIDs. `asOfDate` is an ISO 8601 date (its calendar date is used); omit it for today. Returns one model per team that had one in force that day; unknown teams, and teams with none that day, are left out.")]
    [McpTool("Teams_GetOperatingModelsForTeams", "Get operating models for several teams")]
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
    [EndpointDescription("Takes the team's UUID `id`. The time zone is the one the team's parent team of teams had in effect on the start date (yyyy-MM-dd), else the system default; the commitment grace period is the system default. Use it to fill Teams_SetOperatingModel when the user has not said otherwise.")]
    [McpTool("Teams_GetOperatingModelDefaults", "Get team operating model defaults")]
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
    [EndpointDescription("Takes the team's UUID `id`. True when any of its operating models, past or current, is Scrum, which means it may have sprints to report on. Returns 404 for an unknown team.")]
    [McpTool("Teams_HasEverBeenScrum", "Check whether team has used Scrum")]
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
    [EndpointSummary("Change how a team works from a date, by starting a new operating model.")]
    [EndpointDescription(
        "Takes the team's UUID `id`. The new model starts on `startDate` (yyyy-MM-dd), which must be after the current model's start; the current model is closed the day before. Use it for a real change of methodology, sizing, time zone, working week, holiday calendar or cadence from a date; to fix a model's values for its whole period, use Teams_UpdateOperatingModel. `methodology` is Scrum or Kanban; `sizingMethod` is StoryPoints, Count, Effort or Size; `timeZone` is an IANA id; `commitmentGraceDays` is 0 to 14. `workingDays` (day names such as Monday, at least one) carries over the current model's when omitted. `holidayCalendarId` is a calendar from HolidayCalendars_GetList, or null for the system default. `cadenceWeeks` (1 to 8) with `cadenceAnchor` (yyyy-MM-dd, the first day of one period) set a Kanban team's reporting cadence; omit both for none, and always for Scrum. Teams_GetOperatingModelDefaults gives the values to start from. Returns the new model's id.")]
    [McpTool("Teams_SetOperatingModel", "Set team operating model")]
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
    [EndpointSummary("Correct one of a team's operating models for its whole period.")]
    [EndpointDescription(
        "Takes the team's UUID `id` and the model's `operatingModelId`; any model, current or past, can be corrected, and its dates do not change. The corrected values apply to every date the model covers, so for a change that starts on a date use Teams_SetOperatingModel instead. **This is a whole-record overwrite: an omitted `holidayCalendarId` reverts to the system default and omitted `cadenceWeeks`/`cadenceAnchor` remove the cadence**; only an omitted `workingDays` keeps the model's own. Read the model first with Teams_GetOperatingModel and pass back what should stay. Field rules are as for Teams_SetOperatingModel.")]
    [McpTool("Teams_UpdateOperatingModel", "Correct team operating model")]
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
    [EndpointSummary("Delete a team's current operating model, reinstating the one before it.")]
    [EndpointDescription(
        "Takes the team's UUID `id` and the current model's `operatingModelId`. Only the current model can be deleted, and never the team's only one; the previous model becomes current again with no end date. Use it to undo a model set by mistake.")]
    [McpTool("Teams_DeleteOperatingModel", "Delete team operating model")]
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
    [EndpointDescription(
        "Takes the team's UUID `id`. Every sprint the team has, past, current and future, ordered by start date, each with its `state` (Future, Active or Completed) and sprint type. An unknown team, or a team of teams, returns an empty list. For velocity and predictability, use Teams_GetSprintMetrics.")]
    [McpTool("Teams_GetSprints", "List team sprints")]
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
    [EndpointDescription(
        "Takes the team's UUID `id`. Returns the sprint in progress now, with its dates, sprint type and what the caller may do with it. Returns 404 when the team has no sprint in progress, or the id is not a team's: report that the team has no active sprint, not a failure, and use Teams_GetSprints to find its next or last one.")]
    [McpTool("Teams_GetActiveSprint", "Get team active sprint")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SprintDetailsDto>> GetActiveSprint(Guid id, CancellationToken cancellationToken)
    {
        var sprint = await _dispatcher.Send(new GetTeamActiveSprintQuery(id), cancellationToken);

        return sprint is not null
            ? Ok(sprint)
            : NotFound();
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
    [EndpointSummary("Get the functional organization chart for a given date.")]
    [EndpointDescription(
        "The tree of teams of teams and teams active on `asOfDate` (an ISO 8601 date, its calendar date used; omit for today), placed by their memberships on that day: `organization` holds the top-level units, each with its `id`, `key`, `code`, `type` (Team or Team of Teams), `level` (0 at the top), `path` of codes and `children`; a team with no parent that day is at the top. `total` counts every unit and `maxDepth` is the deepest level. Teams only, not people: for members use Teams_GetMembers.")]
    [McpTool("Teams_GetFunctionalOrganizationChart", "Get functional organization chart")]
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
    [EndpointDescription(
        "Takes the team's UUID `id`. One entry per person, by name, with the `employee` (`id` is the employee UUID the member tools take) and every team member `role` they hold on the team. Membership has no dates; the time people give the team is their allocation, under Teams_GetCapacity. An unknown team returns an empty list.")]
    [McpTool("Teams_GetMembers", "List team members")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TeamMemberDto>>> GetMembers(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _dispatcher.Send(new GetTeamMembersQuery(id), cancellationToken));
    }

    [HttpPost("{id}/members")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Teams)]
    [EndpointSummary("Add a member to a team.")]
    [EndpointDescription(
        "Takes the team's UUID `id`, the `employeeId` (employee UUID) and at least one `roleIds` entry: team member role UUIDs from TeamMemberRoles_GetList. The team and the employee must be active. Adding someone who is already a member adds the roles; a role they already hold on the team is refused. Membership does not allocate their time; that is Employees_ChangeAllocations.")]
    [McpTool("Teams_AddMember", "Add team member", Destructive = false)]
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
    [EndpointDescription(
        "Takes the team's UUID `id` and the member's `employeeId`. **`roleIds` replaces the member's roles on the team**: roles left out are removed, so pass every role they should keep (at least one; to take them off the team, use Teams_RemoveMember). Read the current roles with Teams_GetMembers first.")]
    [McpTool("Teams_UpdateMember", "Change team member roles")]
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
    [EndpointDescription(
        "Takes the team's UUID `id` and the member's `employeeId`, and removes every role they hold on the team. Their allocations to the team are not changed; end those with Employees_ChangeAllocations.")]
    [McpTool("Teams_RemoveMember", "Remove team member")]
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
