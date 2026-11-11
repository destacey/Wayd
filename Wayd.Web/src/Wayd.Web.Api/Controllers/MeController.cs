using Microsoft.AspNetCore.Authorization;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Common.Extensions;
using Wayd.DemandManagement.Application.IntakeDesks.Dtos;
using Wayd.DemandManagement.Application.IntakeDesks.Queries;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Application.Models;
using Wayd.Organization.Application.Teams.Dtos;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Application.PlanningIntervals.Queries;
using Wayd.Planning.Application.Risks.Queries;
using Wayd.ProductManagement.Application.Products.Queries;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Me;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.Iterations.Queries;
using Wayd.Work.Application.WorkItemDependencies.Dtos;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.Application.WorkTeams.Dtos;
using Wayd.Work.Application.WorkTeams.Queries;
using Wayd.Work.Domain.Models.CadenceMetrics;
using Wayd.Work.Domain.Models.Flow;

namespace Wayd.Web.Api.Controllers;

/// <summary>
/// The caller's own records, resolved from who is signed in. Every action needs only a signed-in account:
/// reading your own team memberships and allocations does not take the View Employees permission that
/// reading anyone else's does.
/// </summary>
[Route("api/me")]
[ApiVersionNeutral]
[ApiController]
[Authorize]
[McpTools("wayd-users")]
public class MeController(IDispatcher dispatcher, IUserService userService, ICurrentUser currentUser, ICurrentPrincipal currentPrincipal, IFeatureFlags featureFlags, IDateTimeProvider dateTimeProvider) : ControllerBase
{
    /// <summary>How many days, today included, a Kanban team's recent completions are counted over.</summary>
    private const int RecentFlowDays = 14;

    private const string NoLinkedEmployee = "Your account is not linked to an employee, so nothing in Wayd is recorded against you as a person. An administrator links an account to an employee on its user record in Settings.";

    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly IUserService _userService = userService;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IFeatureFlags _featureFlags = featureFlags;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    [HttpGet]
    [EndpointSummary("Get who you are: your user account and the employee it is linked to.")]
    [EndpointDescription(
        "Call this first whenever a request is about the person asking (\"my\", \"me\", \"I\"): never list users or employees to find yourself, which needs permissions most people do not hold. Wayd records people as employees, so `employee.id` is the UUID that employeeId, role and assignee parameters take for you, and `userId` is only for parameters that say they take a user id. `employee` is null when your account is not linked to an employee. For your teams, allocations and work items use Me_GetTeamMemberships, Me_GetAllocations and Me_GetWorkItems; for projects where you hold a role, Projects_GetProjects with `role` and no `employeeId`.")]
    [McpTool("Me_GetProfile", "Get who I am")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeResponse>> GetProfile(CancellationToken cancellationToken)
    {
        if (_currentUser.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (await _userService.GetAsync(userId, cancellationToken) is not { } user)
            return Unauthorized();

        return Ok(new MeResponse
        {
            UserId = user.Id,
            UserName = user.UserName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Employee = user.Employee,
            Roles = [.. user.Roles.Select(r => r.Name).Order(StringComparer.OrdinalIgnoreCase)],
        });
    }

    [HttpGet("team-memberships")]
    [EndpointSummary("List the teams and teams of teams you are a member of.")]
    [EndpointDescription(
        "Returns one entry per team or team of teams your employee belongs to, sorted by team name, each with the member roles you hold on it. Needs no View Employees permission. Membership is not allocation: Me_GetAllocations says how much of your time each team has. Returns 404 when your account is not linked to an employee.")]
    [McpTool("Me_GetTeamMemberships", "List my teams")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<TeamMemberDto>>> GetTeamMemberships(CancellationToken cancellationToken)
    {
        if (await _currentPrincipal.GetEmployeeId(cancellationToken) is not { } employeeId)
            return NotLinked();

        var memberships = await _dispatcher.Send(new GetEmployeeTeamMembershipsQuery(employeeId), cancellationToken);

        return memberships is not null
            ? Ok(memberships)
            : NotLinked();
    }

    [HttpGet("allocations")]
    [FeatureGate(FeatureFlags.Names.TeamCapacityManagement)]
    [EndpointSummary("Get how your time is allocated to teams over time.")]
    [EndpointDescription(
        "Returns every allocation of your employee across all teams in date order: the team, the role the time is spent in when one was chosen, the date range (`end` is null when it has no end) and the `percent` of your time. Needs no View Employees permission. Requires the team-capacity-management feature flag; returns 404 when it is off or your account is not linked to an employee.")]
    [McpTool("Me_GetAllocations", "Get my allocations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EmployeeAllocationDto>>> GetAllocations(CancellationToken cancellationToken)
    {
        if (await _currentPrincipal.GetEmployeeId(cancellationToken) is not { } employeeId)
            return NotLinked();

        var allocations = await _dispatcher.Send(new GetEmployeeAllocationsQuery(employeeId), cancellationToken);

        return allocations is not null
            ? Ok(allocations)
            : NotLinked();
    }

    [HttpGet("work-items")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.WorkItems)]
    [EndpointSummary("Get the work items assigned to you.")]
    [EndpointDescription(
        "Your tickets: the backlog-level (Requirement tier) work items assigned to your employee across every workspace; epics, features and tasks are left out. `statusCategories` narrows them to any of the codes `Proposed`, `Active`, `Done` and `Removed` — pass `Proposed` and `Active` for open work. `doneFrom` and `doneTo` keep only work items finished in that window, so either one leaves out work not yet done; a time without a zone is read as UTC. Needs the Work Items view permission. Returns 404 when your account is not linked to an employee.")]
    [McpTool("Me_GetWorkItems", "List my work items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<WorkItemListDto>>> GetWorkItems(
        [FromQuery] StatusCategory[]? statusCategories,
        [FromQuery] DateTime? doneFrom,
        [FromQuery] DateTime? doneTo,
        CancellationToken cancellationToken)
    {
        if (await _currentPrincipal.GetEmployeeId(cancellationToken) is not { } employeeId)
            return NotLinked();

        return Ok(await _dispatcher.Send(
            new GetEmployeeWorkItemsQuery(employeeId, statusCategories, UtcQueryTime.ToInstant(doneFrom), UtcQueryTime.ToInstant(doneTo)),
            cancellationToken));
    }

    [HttpGet("home-context")]
    [EndpointSummary("Get what you are involved in: counts of your teams, projects, intake desks, requests, risks and products.")]
    [EndpointDescription(
        "One call for an overview of where you work: active teams and teams of teams you are a member of; approved and active projects you hold a role on, by role; intake desks you are on, by role, and open requests you raised; open risks assigned to you; and proposed or active products your teams are linked to. Counts only, worked out from your records each time. Needs no permission beyond signing in. An area whose module's feature flag is off counts zero. Returns zeros with `hasLinkedEmployee` false, not 404, when your account is not linked to an employee. To list the records behind a count use Me_GetTeamMemberships, Projects_GetProjects with `role`, IntakeRequests_GetList with `mine` and `openOnly`, or Risks_GetMyRisks.")]
    [McpTool("Me_GetHomeContext", "Get what I am involved in")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<HomeContextResponse>> GetHomeContext(CancellationToken cancellationToken)
    {
        if (await _currentPrincipal.GetEmployeeId(cancellationToken) is not { } employeeId)
            return Ok(new HomeContextResponse());

        var teams = await _dispatcher.Send(new GetEmployeeTeamInvolvementQuery(employeeId), cancellationToken);
        var projects = await _dispatcher.Send(new GetMyProjectsSummaryQuery(StatusFilter: [ProjectStatus.Approved, ProjectStatus.Active]), cancellationToken);
        var riskCount = await _dispatcher.Send(new GetEmployeeOpenRiskCountQuery(employeeId), cancellationToken);

        var intake = await _featureFlags.IsEnabled(FeatureFlags.Names.DemandManagement, cancellationToken)
            ? await _dispatcher.Send(new GetEmployeeIntakeInvolvementQuery(employeeId), cancellationToken)
            : new EmployeeIntakeInvolvementDto();

        var productCount = await _featureFlags.IsEnabled(FeatureFlags.Names.ProductManagement, cancellationToken)
            ? await _dispatcher.Send(new GetTeamsProductCountQuery(teams.TeamIds), cancellationToken)
            : 0;

        return Ok(new HomeContextResponse
        {
            HasLinkedEmployee = true,
            Teams = new HomeTeamsContextResponse { TeamCount = teams.TeamCount, TeamOfTeamsCount = teams.TeamOfTeamsCount },
            Projects = projects ?? new MyProjectsSummaryDto(),
            Intake = intake,
            AssignedRiskCount = riskCount,
            TeamProductCount = productCount,
        });
    }

    [HttpGet("teams/summary")]
    [EndpointSummary("Get a summary of each team and team of teams you are a member of, for how it works today.")]
    [EndpointDescription(
        "`teams` has one entry per active team you are a member of, by name, with the methodology in effect today in the team's own time zone. A Scrum team has `sprint` with its active sprint, null between sprints; a Kanban team has `flow`: its current reporting period when it has a cadence, items in progress, items finished over the last 14 days with their median cycle time, and the item in progress longest. Every team has its open `dependencies` by planning health, its `openRiskCount`, and `partOf`, the team of teams it belongs to today. A team with no operating model today has neither `sprint` nor `flow`. "
        + "`teamsOfTeams` has one rollup per active team of teams you are a member of directly (being on one of its teams does not count), by name: the planning interval running today with its objectives at risk, and one row per team or team of teams in it today, needing attention first. A Scrum row's `sprint` is a sprint started and left open past its end, or its running sprint's share done against the share its burn-down expects; a Kanban row's `flow` is its items in progress and when the oldest started; a nested team of teams counts the teams beneath it by status and takes the worst. Each row's `status` (OnTrack, Watch, NeedsAttention) is worked out here. Its `dependencies` are the open cross-team dependencies of every team beneath it, and its `risks` its own open risks and those of the teams beneath it. "
        + "Each section is null without the permission it needs: View Iterations for `sprint`, View Work Items for `flow` and `dependencies`, View Risks for `openRiskCount` and `risks`, View Planning Intervals for `planningInterval` and View Planning Interval Objectives for its at-risk count. Returns 404 when your account is not linked to an employee.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyTeamsSummaryResponse>> GetTeamsSummary(CancellationToken cancellationToken)
    {
        if (await _currentPrincipal.GetEmployeeId(cancellationToken) is not { } employeeId)
            return NotLinked();

        var access = new SummaryAccess(
            Iterations: await CanView(ApplicationResource.Iterations, cancellationToken),
            WorkItems: await CanView(ApplicationResource.WorkItems, cancellationToken),
            Risks: await CanView(ApplicationResource.Risks, cancellationToken),
            PlanningIntervals: await CanView(ApplicationResource.PlanningIntervals, cancellationToken),
            Objectives: await CanView(ApplicationResource.PlanningIntervalObjectives, cancellationToken));

        var teams = await _dispatcher.Send(new GetEmployeeActiveTeamsQuery(employeeId), cancellationToken);
        var teamsOfTeams = await _dispatcher.Send(new GetEmployeeActiveTeamsQuery(employeeId, TeamType.TeamOfTeams), cancellationToken);
        if (teams.Count == 0 && teamsOfTeams.Count == 0)
        {
            return Ok(new MyTeamsSummaryResponse
            {
                DependenciesAtRiskCount = access.WorkItems ? 0 : null,
                UnhealthyCrossTeamDependencyCount = access.WorkItems ? 0 : null,
                SprintsEndedNotCompletedCount = access.Iterations ? 0 : null,
            });
        }

        var now = _dateTimeProvider.Now;
        var today = _dateTimeProvider.Today;

        var structures = new List<TeamStructure>();
        foreach (var teamOfTeams in teamsOfTeams)
        {
            if (await _dispatcher.Send(new GetTeamStructureQuery(teamOfTeams.Id, today, today), cancellationToken) is { } structure)
                structures.Add(structure);
        }

        // Every team read for a signal: the caller's own, and those beneath their teams of teams.
        var teamIds = teams.Select(t => t.Id).ToList();
        var rollupTeamIds = structures
            .SelectMany(s => s.Teams.Where(t => t.Type == TeamType.Team).Select(t => t.Id))
            .Distinct()
            .ToList();
        var signalTeamIds = teamIds.Union(rollupTeamIds).ToList();

        var schedules = await _dispatcher.Send(new GetTeamsScheduleHistoryQuery(signalTeamIds), cancellationToken);
        var models = signalTeamIds.ToDictionary(id => id, id => (schedules.GetValueOrDefault(id) ?? []).InEffectAt(now));

        List<Guid> Following(IEnumerable<Guid> ids, Methodology methodology) =>
            [.. ids.Where(id => models[id]?.Methodology == methodology)];

        var scrumTeamIds = Following(teamIds, Methodology.Scrum);
        var activeSprints = access.Iterations && scrumTeamIds.Count > 0
            ? await _dispatcher.Send(new GetTeamsActiveSprintQuery(scrumTeamIds), cancellationToken)
            : (IReadOnlyDictionary<Guid, NavigationDto>)new Dictionary<Guid, NavigationDto>();

        var rollupScrumTeamIds = Following(rollupTeamIds, Methodology.Scrum);
        var sprintSignals = access.Iterations && rollupScrumTeamIds.Count > 0
            ? await _dispatcher.Send(new GetTeamsSprintSignalQuery(rollupScrumTeamIds), cancellationToken)
            : (IReadOnlyDictionary<Guid, TeamSprintSignalDto>)new Dictionary<Guid, TeamSprintSignalDto>();

        var kanbanTeamIds = Following(signalTeamIds, Methodology.Kanban);
        var flows = access.WorkItems && kanbanTeamIds.Count > 0
            ? await LoadFlows(kanbanTeamIds, schedules, models, now, cancellationToken)
            : new Dictionary<Guid, MyTeamFlowSummaryResponse>();

        var dependencies = access.WorkItems && teamIds.Count > 0
            ? await _dispatcher.Send(new GetTeamsDependencyHealthQuery(teamIds), cancellationToken)
            : null;

        var riskTeamIds = teamIds.Union(structures.SelectMany(s => s.Teams.Select(t => t.Id))).ToList();
        var riskCounts = access.Risks
            ? await _dispatcher.Send(new GetTeamsOpenRiskCountQuery(riskTeamIds), cancellationToken)
            : null;

        var parents = teamIds.Count > 0
            ? await _dispatcher.Send(new GetTeamsParentQuery(teamIds, today), cancellationToken)
            : new Dictionary<Guid, TeamNavigationDto>();

        var signals = new TeamSignals(models, sprintSignals, flows, now);
        var rollups = new List<MyTeamOfTeamsSummaryResponse>();
        var crossTeamDependencies = new Dictionary<Guid, DependencyDto>();
        foreach (var (teamOfTeams, structure) in teamsOfTeams.Join(structures, t => t.Id, s => s.RootId, (t, s) => (t, s)))
        {
            var openDependencies = access.WorkItems
                ? await LoadCrossTeamDependencies(structure, cancellationToken)
                : null;
            foreach (var dependency in openDependencies ?? [])
                crossTeamDependencies.TryAdd(dependency.Id, dependency);

            var planningInterval = access.PlanningIntervals
                ? await _dispatcher.Send(new GetCurrentPlanningIntervalForTeamsQuery(structure.RootId, [.. structure.Teams.Select(t => t.Id)], today, access.Objectives), cancellationToken)
                : null;

            rollups.Add(BuildRollup(teamOfTeams, structure, [.. teamIds, .. teamsOfTeams.Select(t => t.Id)], signals, openDependencies, riskCounts, planningInterval, access));
        }

        return Ok(new MyTeamsSummaryResponse
        {
            Teams = [.. teams.Select(team =>
            {
                var model = models[team.Id];
                return new MyTeamSummaryResponse
                {
                    Team = team,
                    PartOf = parents.GetValueOrDefault(team.Id),
                    Methodology = model?.Methodology,
                    TimeZone = model?.TimeZone,
                    Sprint = access.Iterations && model?.Methodology == Methodology.Scrum
                        ? new MyTeamSprintSummaryResponse { ActiveSprint = activeSprints.GetValueOrDefault(team.Id) }
                        : null,
                    Flow = flows.GetValueOrDefault(team.Id),
                    Dependencies = dependencies is null ? null : dependencies.Teams.GetValueOrDefault(team.Id) ?? new TeamDependencyHealthDto(),
                    OpenRiskCount = riskCounts is null ? null : riskCounts.GetValueOrDefault(team.Id),
                };
            })],
            TeamsOfTeams = rollups,
            DependenciesAtRiskCount = access.WorkItems ? dependencies?.AtRiskOrUnhealthyCount ?? 0 : null,
            UnhealthyCrossTeamDependencyCount = access.WorkItems
                ? crossTeamDependencies.Values.Count(d => d.Health.Id == (int)DependencyPlanningHealth.Unhealthy)
                : null,
            SprintsEndedNotCompletedCount = access.Iterations
                ? sprintSignals.Values.Where(s => s.EndedNotCompleted).Select(s => s.Sprint.Id).Distinct().Count()
                : null,
        });
    }

    /// <summary>The summary sections the caller may see, each needing the permission its own page needs.</summary>
    private sealed record SummaryAccess(bool Iterations, bool WorkItems, bool Risks, bool PlanningIntervals, bool Objectives);

    /// <summary>What each team's row in a rollup is read from.</summary>
    private sealed record TeamSignals(
        IReadOnlyDictionary<Guid, TeamSchedulePeriodDto?> Models,
        IReadOnlyDictionary<Guid, TeamSprintSignalDto> Sprints,
        IReadOnlyDictionary<Guid, MyTeamFlowSummaryResponse> Flows,
        Instant Now);

    /// <summary>Whether the caller holds the View permission on <paramref name="resource"/>.</summary>
    private async Task<bool> CanView(string resource, CancellationToken cancellationToken) =>
        await _currentPrincipal.HasPermission(ApplicationPermission.NameFor(ApplicationAction.View, resource), cancellationToken);

    /// <summary>
    /// The open cross-team dependencies of every team beneath a team of teams, as its Dependencies section reads
    /// them (TeamsOfTeams_GetDependencies).
    /// </summary>
    private async Task<List<DependencyDto>> LoadCrossTeamDependencies(TeamStructure structure, CancellationToken cancellationToken)
    {
        var teamIds = structure.Teams.Where(t => t.Type == TeamType.Team).Select(t => t.Id).ToList();
        return teamIds.Count == 0
            ? []
            : await _dispatcher.Send(new GetTeamsDependenciesQuery(teamIds, [DependencyState.ToDo, DependencyState.InProgress]), cancellationToken);
    }

    /// <summary>
    /// A team of teams' rollup: one row per team or team of teams in it today, a nested one carrying the worst
    /// status of the teams beneath it at any depth.
    /// </summary>
    private static MyTeamOfTeamsSummaryResponse BuildRollup(
        TeamNavigationDto teamOfTeams,
        TeamStructure structure,
        HashSet<Guid> callerTeamIds,
        TeamSignals signals,
        List<DependencyDto>? dependencies,
        IReadOnlyDictionary<Guid, int>? riskCounts,
        CurrentPlanningIntervalDto? planningInterval,
        SummaryAccess access)
    {
        var teamsById = structure.Teams.ToDictionary(t => t.Id);
        var childIdsByParent = structure.Memberships.ToLookup(m => m.ParentId, m => m.ChildId);

        List<Guid> TeamsBeneath(Guid parentId)
        {
            var found = new List<Guid>();
            var visited = new HashSet<Guid> { parentId };
            var pending = new Queue<Guid>([parentId]);
            while (pending.TryDequeue(out var id))
            {
                foreach (var childId in childIdsByParent[id].Where(visited.Add))
                {
                    pending.Enqueue(childId);
                    if (teamsById.TryGetValue(childId, out var child) && child.Type == TeamType.Team)
                        found.Add(childId);
                }
            }
            return found;
        }

        var rows = childIdsByParent[structure.RootId]
            .Distinct()
            .Where(teamsById.ContainsKey)
            .Select(childId =>
            {
                var child = teamsById[childId];
                var navigation = new TeamNavigationDto { Id = child.Id, Key = child.Key, Name = child.Name, Code = child.Code, Type = child.Type.GetDisplayName() };

                if (child.Type == TeamType.TeamOfTeams)
                {
                    var statuses = TeamsBeneath(child.Id).Select(id => TeamStatus(id, signals)).ToList();
                    return new MyTeamOfTeamsRowResponse
                    {
                        Team = navigation,
                        IsMine = callerTeamIds.Contains(child.Id),
                        Nested = new MyTeamOfTeamsNestedResponse
                        {
                            TeamCount = statuses.Count,
                            NeedsAttentionCount = statuses.Count(s => s == AttentionStatus.NeedsAttention),
                            WatchCount = statuses.Count(s => s == AttentionStatus.Watch),
                        },
                        Status = statuses.Count == 0 ? AttentionStatus.OnTrack : statuses.Max(),
                    };
                }

                var methodology = signals.Models.GetValueOrDefault(child.Id)?.Methodology;
                var flow = methodology == Methodology.Kanban ? signals.Flows.GetValueOrDefault(child.Id) : null;
                return new MyTeamOfTeamsRowResponse
                {
                    Team = navigation,
                    IsMine = callerTeamIds.Contains(child.Id),
                    Methodology = methodology,
                    Sprint = methodology == Methodology.Scrum ? signals.Sprints.GetValueOrDefault(child.Id) : null,
                    Flow = flow is null ? null : new MyTeamOfTeamsFlowResponse
                    {
                        InProgressCount = flow.InProgressCount,
                        OldestInProgressSince = flow.OldestInProgress?.ActivatedOn,
                        MedianCycleTimeDays = flow.MedianCycleTimeDays,
                    },
                    Status = TeamStatus(child.Id, signals),
                };
            })
            .OrderByDescending(r => r.Status)
            .ThenBy(r => r.Team.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Team.Id)
            .ToList();

        return new MyTeamOfTeamsSummaryResponse
        {
            Team = teamOfTeams,
            ScrumTeamCount = rows.Count(r => r.Methodology == Methodology.Scrum),
            KanbanTeamCount = rows.Count(r => r.Methodology == Methodology.Kanban),
            PlanningInterval = planningInterval is null ? null : new MyTeamOfTeamsPlanningIntervalResponse
            {
                PlanningInterval = planningInterval.PlanningInterval,
                ObjectivesAtRiskCount = planningInterval.ObjectivesAtRiskCount,
            },
            Rows = rows,
            Dependencies = dependencies is null ? null : new MyTeamOfTeamsDependenciesResponse
            {
                OpenCount = dependencies.Count,
                AtRiskCount = dependencies.Count(d => d.Health.Id == (int)DependencyPlanningHealth.AtRisk),
                UnhealthyCount = dependencies.Count(d => d.Health.Id == (int)DependencyPlanningHealth.Unhealthy),
            },
            Risks = riskCounts is null ? null : new MyTeamOfTeamsRisksResponse
            {
                OwnOpenCount = riskCounts.GetValueOrDefault(structure.RootId),
                TeamsOpenCount = structure.Teams.Where(t => t.Id != structure.RootId).Sum(t => riskCounts.GetValueOrDefault(t.Id)),
            },
        };
    }

    /// <summary>
    /// A team's status from the signal of the methodology it follows today: its sprint for Scrum, the age of its
    /// oldest item in progress for Kanban. On Track with no signal to read, or without the permission to read it.
    /// </summary>
    private static AttentionStatus TeamStatus(Guid teamId, TeamSignals signals) =>
        signals.Models.GetValueOrDefault(teamId)?.Methodology switch
        {
            Methodology.Scrum => signals.Sprints.GetValueOrDefault(teamId)?.Status ?? AttentionStatus.OnTrack,
            Methodology.Kanban when signals.Flows.GetValueOrDefault(teamId) is { } flow => FlowAging.Assess(
                flow.OldestInProgress is { } oldest ? (signals.Now - oldest.ActivatedOn).TotalDays : null,
                flow.MedianCycleTimeDays),
            _ => AttentionStatus.OnTrack,
        };

    /// <summary>
    /// The flow of each Kanban team: completions over the last <see cref="RecentFlowDays"/> days in the team's own
    /// zone, counted only from the day it became Kanban, so teams that share those dates are measured in one query.
    /// </summary>
    private async Task<Dictionary<Guid, MyTeamFlowSummaryResponse>> LoadFlows(
        List<Guid> kanbanTeamIds,
        IReadOnlyDictionary<Guid, IReadOnlyList<TeamSchedulePeriodDto>> schedules,
        Dictionary<Guid, TeamSchedulePeriodDto?> models,
        Instant now,
        CancellationToken cancellationToken)
    {
        var windows = kanbanTeamIds.Select(teamId =>
        {
            var model = models[teamId]!;
            var today = now.InZone(model.Zone()).Date;
            var start = today.PlusDays(1 - RecentFlowDays);
            var kanbanSince = KanbanSince(schedules.GetValueOrDefault(teamId) ?? [], model);
            return (TeamId: teamId, Start: kanbanSince > start ? kanbanSince : start, End: today);
        }).ToList();

        var metrics = new List<KanbanTeamWindowMetricsDto>();
        foreach (var group in windows.GroupBy(w => (w.Start, w.End)))
            metrics.AddRange(await _dispatcher.Send(new GetKanbanTeamsWindowMetricsQuery([.. group.Select(w => w.TeamId)], group.Key.Start, group.Key.End), cancellationToken));

        var oldest = await _dispatcher.Send(new GetTeamsOldestInProgressWorkItemQuery(kanbanTeamIds), cancellationToken);

        return windows.ToDictionary(w => w.TeamId, w =>
        {
            var measured = metrics.FirstOrDefault(m => m.Team.Id == w.TeamId);
            return new MyTeamFlowSummaryResponse
            {
                CurrentPeriod = CurrentPeriod(schedules.GetValueOrDefault(w.TeamId) ?? [], now),
                InProgressCount = measured?.InProgressWorkItems ?? 0,
                CompletedSince = w.Start,
                CompletedCount = measured?.CompletedWorkItems ?? 0,
                MedianCycleTimeDays = measured?.MedianCycleTimeDays,
                OldestInProgress = oldest.GetValueOrDefault(w.TeamId),
            };
        });
    }

    /// <summary>
    /// The day the team became Kanban: the start of the unbroken run of Kanban models that ends with
    /// <paramref name="current"/>. Any edit to a team's operating model starts a new model, so the current one's
    /// start alone would cut the window short at, say, a change of sizing method.
    /// </summary>
    private static LocalDate KanbanSince(IReadOnlyList<TeamSchedulePeriodDto> schedule, TeamSchedulePeriodDto current)
    {
        var since = current.Start;
        foreach (var period in schedule.Where(p => p.Start < current.Start).OrderByDescending(p => p.Start))
        {
            if (period.Methodology != Methodology.Kanban || period.End != since.PlusDays(-1))
                break;
            since = period.Start;
        }

        return since;
    }

    /// <summary>The reporting period running now, as the team's Velocity report counts it; null without a cadence.</summary>
    private static MyTeamReportingPeriodResponse? CurrentPeriod(IReadOnlyList<TeamSchedulePeriodDto> schedule, Instant now)
    {
        var current = CadencePeriod.Recent(
                schedule
                    .Where(p => p.Cadence is not null)
                    .Select(p => new CadenceSchedulePeriod(p.Start, p.End, p.TimeZone, p.SizingMethod, p.Cadence!)),
                now,
                window: 1)
            .FirstOrDefault(p => p.IsCurrent);

        return current is null ? null : new MyTeamReportingPeriodResponse { Start = current.Start, End = current.End };
    }

    private NotFoundObjectResult NotLinked() =>
        NotFound(ProblemDetailsExtensions.ForNotFound(NoLinkedEmployee, HttpContext));
}
