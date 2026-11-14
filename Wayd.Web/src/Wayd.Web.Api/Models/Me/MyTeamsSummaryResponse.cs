using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Organization.Application.Models;
using Wayd.Work.Application.Iterations.Dtos;
using Wayd.Work.Application.WorkItemDependencies.Dtos;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Web.Api.Models.Me;

/// <summary>
/// The active teams and teams of teams the caller is a member of directly, each summarised for how it works today.
/// A section is null when the caller lacks the permission the team or team of teams page needs to show it.
/// </summary>
public sealed record MyTeamsSummaryResponse
{
    /// <summary>One entry per team, by team name.</summary>
    public IReadOnlyList<MyTeamSummaryResponse> Teams { get; init; } = [];

    /// <summary>
    /// One rollup per team of teams the caller is a member of directly, by name. Being on one of its teams does not
    /// make the caller a member of it.
    /// </summary>
    public IReadOnlyList<MyTeamOfTeamsSummaryResponse> TeamsOfTeams { get; init; } = [];

    /// <summary>
    /// Every active team the caller is a member of directly, by name, whether or not `teams` was narrowed to some
    /// of them: what a choice of teams is made from.
    /// </summary>
    public IReadOnlyList<TeamNavigationDto> AllTeams { get; init; } = [];

    /// <summary>
    /// Every active team of teams the caller is a member of directly, by name, whether or not `teamsOfTeams` was
    /// narrowed to some of them.
    /// </summary>
    public IReadOnlyList<TeamNavigationDto> AllTeamsOfTeams { get; init; } = [];

    /// <summary>
    /// The open cross-team dependencies that are unhealthy across the caller's teams of teams, each counted once
    /// even when two of them share it. Null without the View Work Items permission.
    /// </summary>
    public int? UnhealthyCrossTeamDependencyCount { get; init; }

    /// <summary>
    /// The sprints of the teams in the caller's teams of teams that were started and are past their end without
    /// being completed. Null without the View Iterations permission.
    /// </summary>
    public int? SprintsEndedNotCompletedCount { get; init; }

    /// <summary>
    /// The open dependencies at risk or unhealthy across the teams, each counted once even when it joins two of them.
    /// Null without the View Work Items permission.
    /// </summary>
    public int? DependenciesAtRiskCount { get; init; }
}

/// <summary>One of the caller's teams, summarised for the methodology it follows today.</summary>
public sealed record MyTeamSummaryResponse
{
    /// <summary>The team.</summary>
    public required TeamNavigationDto Team { get; init; }

    /// <summary>The team of teams the team belongs to today; null when it belongs to none.</summary>
    public TeamNavigationDto? PartOf { get; init; }

    /// <summary>
    /// How the team works today, read from the operating model that covers today's date in the team's own time
    /// zone. Null when no operating model covers today, in which case neither `sprint` nor
    /// `flow` is returned.
    /// </summary>
    public Methodology? Methodology { get; init; }

    /// <summary>The IANA time zone of today's operating model, which the team's dates are counted in; null with no model.</summary>
    public string? TimeZone { get; init; }

    /// <summary>A Scrum team's sprint. Null for any other team, or without the View Iterations permission.</summary>
    public MyTeamSprintSummaryResponse? Sprint { get; init; }

    /// <summary>A Kanban team's flow. Null for any other team, or without the View Work Items permission.</summary>
    public MyTeamFlowSummaryResponse? Flow { get; init; }

    /// <summary>
    /// The team's open dependencies by planning health, counted as its Dependencies section lists them, including
    /// those within the team. Null without the View Work Items permission.
    /// </summary>
    public TeamDependencyHealthDto? Dependencies { get; init; }

    /// <summary>How many open risks the team has. Null without the View Risks permission.</summary>
    public int? OpenRiskCount { get; init; }
}

/// <summary>A Scrum team's sprint today.</summary>
public sealed record MyTeamSprintSummaryResponse
{
    /// <summary>The sprint the team is running now, or null between sprints.</summary>
    public NavigationDto? ActiveSprint { get; init; }
}

/// <summary>A Kanban team's flow: what it has in progress, and what it finished lately.</summary>
public sealed record MyTeamFlowSummaryResponse
{
    /// <summary>The team's current reporting period, or null when the team has no reporting cadence.</summary>
    public MyTeamReportingPeriodResponse? CurrentPeriod { get; init; }

    /// <summary>Requirement-tier work items in an In Progress-category status now.</summary>
    public int InProgressCount { get; init; }

    /// <summary>
    /// The first day `completedCount` counts from, in the team's time zone: 13 days before today, or
    /// the day the team became Kanban when that is later.
    /// </summary>
    public LocalDate CompletedSince { get; init; }

    /// <summary>Requirement-tier work items finished from `completedSince` through today.</summary>
    public int CompletedCount { get; init; }

    /// <summary>The median cycle time in days of the items `completedCount` counts; null when none has one.</summary>
    public double? MedianCycleTimeDays { get; init; }

    /// <summary>The work item in progress longest, or null when nothing in progress has a start time.</summary>
    public InProgressWorkItemDto? OldestInProgress { get; init; }
}

/// <summary>A reporting period of a Kanban team's cadence.</summary>
public sealed record MyTeamReportingPeriodResponse
{
    /// <summary>The period's first day.</summary>
    public LocalDate Start { get; init; }

    /// <summary>The period's last day, included in it.</summary>
    public LocalDate End { get; init; }
}

/// <summary>
/// A team of teams the caller belongs to directly, rolled up from the teams beneath it: one row for each team or
/// team of teams in it today, with the ones needing attention first.
/// </summary>
public sealed record MyTeamOfTeamsSummaryResponse
{
    /// <summary>The team of teams.</summary>
    public required TeamNavigationDto Team { get; init; }

    /// <summary>How many of the rows are teams following Scrum today.</summary>
    public int ScrumTeamCount { get; init; }

    /// <summary>How many of the rows are teams following Kanban today.</summary>
    public int KanbanTeamCount { get; init; }

    /// <summary>
    /// The planning interval running today that the team of teams, or a team beneath it, takes part in. Null when
    /// none is running, or without the View Planning Intervals permission.
    /// </summary>
    public MyTeamOfTeamsPlanningIntervalResponse? PlanningInterval { get; init; }

    /// <summary>
    /// One row for each team and team of teams in it today, by membership: needing attention first, then watch,
    /// then on track, each by name.
    /// </summary>
    public IReadOnlyList<MyTeamOfTeamsRowResponse> Rows { get; init; } = [];

    /// <summary>
    /// Its open cross-team dependencies, as its Dependencies section lists them: those of every team beneath it, at
    /// any depth, whose predecessor is not done. Null without the View Work Items permission.
    /// </summary>
    public MyTeamOfTeamsDependenciesResponse? Dependencies { get; init; }

    /// <summary>Its open risks and those of the teams beneath it. Null without the View Risks permission.</summary>
    public MyTeamOfTeamsRisksResponse? Risks { get; init; }
}

/// <summary>The planning interval a team of teams is in today.</summary>
public sealed record MyTeamOfTeamsPlanningIntervalResponse
{
    /// <summary>The planning interval.</summary>
    public required NavigationDto PlanningInterval { get; init; }

    /// <summary>
    /// Open objectives of the team of teams and every team beneath it whose current health check is At Risk or
    /// Unhealthy. Null without the View Planning Interval Objectives permission.
    /// </summary>
    public int? ObjectivesAtRiskCount { get; init; }
}

/// <summary>
/// A team, or a nested team of teams, in a team of teams' rollup, with one signal for how it is doing today.
/// </summary>
public sealed record MyTeamOfTeamsRowResponse
{
    /// <summary>The team or team of teams.</summary>
    public required TeamNavigationDto Team { get; init; }

    /// <summary>Whether the caller is a member of this team or team of teams directly.</summary>
    public bool IsMine { get; init; }

    /// <summary>A team's methodology today; null for a team of teams, or a team with no operating model today.</summary>
    public Methodology? Methodology { get; init; }

    /// <summary>
    /// A Scrum team's sprint signal: a sprint left open past its end, or its running sprint's progress. Null
    /// between sprints, for any other row, or without the View Iterations permission.
    /// </summary>
    public TeamSprintSignalDto? Sprint { get; init; }

    /// <summary>A Kanban team's work in progress. Null for any other row, or without the View Work Items permission.</summary>
    public MyTeamOfTeamsFlowResponse? Flow { get; init; }

    /// <summary>
    /// A nested team of teams' teams, at any depth, counted by status; its own status is the worst of them. Null
    /// for a team.
    /// </summary>
    public MyTeamOfTeamsNestedResponse? Nested { get; init; }

    /// <summary>
    /// How much the row calls for attention: from its sprint or flow for a team, the worst beneath it for a team of
    /// teams. On Track when there is no signal to read — including when the caller may not view the sprint or flow
    /// it would come from, so it is no signal on its own then.
    /// </summary>
    public AttentionStatus Status { get; init; }
}

/// <summary>A Kanban team's work in progress, and whether it is getting stuck.</summary>
public sealed record MyTeamOfTeamsFlowResponse
{
    /// <summary>Requirement-tier work items in an In Progress-category status now.</summary>
    public int InProgressCount { get; init; }

    /// <summary>When the item in progress longest started; null when nothing in progress has a start time.</summary>
    public Instant? OldestInProgressSince { get; init; }

    /// <summary>
    /// The team's median cycle time in days over the last 14 days, which the oldest item's age is weighed against;
    /// null when it finished nothing in that time.
    /// </summary>
    public double? MedianCycleTimeDays { get; init; }
}

/// <summary>The teams beneath a nested team of teams, counted by status.</summary>
public sealed record MyTeamOfTeamsNestedResponse
{
    /// <summary>How many teams are beneath it today, at any depth.</summary>
    public int TeamCount { get; init; }

    /// <summary>How many of them need attention.</summary>
    public int NeedsAttentionCount { get; init; }

    /// <summary>How many of them are on watch.</summary>
    public int WatchCount { get; init; }
}

/// <summary>A team of teams' open cross-team dependencies, by planning health.</summary>
public sealed record MyTeamOfTeamsDependenciesResponse
{
    /// <summary>The open cross-team dependencies.</summary>
    public int OpenCount { get; init; }

    /// <summary>Those whose ends are both unplanned.</summary>
    public int AtRiskCount { get; init; }

    /// <summary>Those planned to finish after they are needed, or whose successor started first.</summary>
    public int UnhealthyCount { get; init; }
}

/// <summary>A team of teams' open risks.</summary>
public sealed record MyTeamOfTeamsRisksResponse
{
    /// <summary>Open risks raised on the team of teams itself, which its Risks section lists.</summary>
    public int OwnOpenCount { get; init; }

    /// <summary>Open risks raised on the teams and teams of teams beneath it today, at any depth.</summary>
    public int TeamsOpenCount { get; init; }
}
