using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Organization.Application.Models;
using Wayd.Work.Application.WorkItemDependencies.Dtos;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Web.Api.Models.Me;

/// <summary>
/// The active teams the caller is a member of, each summarised for how it works today. Teams of teams are not
/// included. A section is null when the caller lacks the permission the team page needs to show it.
/// </summary>
public sealed record MyTeamsSummaryResponse
{
    /// <summary>One entry per team, by team name.</summary>
    public IReadOnlyList<MyTeamSummaryResponse> Teams { get; init; } = [];

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
