using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Web.Api.Dtos.Ppm;

/// <summary>
/// Everything a project health-check recommendation weighs, gathered in one read. Classifying it and
/// drafting the note are left to the reader.
/// </summary>
public sealed record ProjectHealthCheckEvidenceDto
{
    /// <summary>The date the evidence was gathered on, which overdue and past-end are measured against.</summary>
    public LocalDate AsOf { get; init; }

    /// <summary>The project: status, committed start and end, current health check and role lists.</summary>
    public required ProjectDetailsDto Project { get; init; }

    /// <summary>Overdue, due this week, upcoming and total leaf task counts across the whole plan.</summary>
    public required ProjectPlanSummaryDto PlanSummary { get; init; }

    /// <summary>
    /// The open tasks (Not Started or In Progress) whose planned end has passed — the ones the plan
    /// summary counts as overdue — with their keys, so a note can cite them.
    /// </summary>
    public List<ProjectTaskListDto> OverdueTasks { get; init; } = [];

    /// <summary>The project's stages in order; empty when no lifecycle is assigned.</summary>
    public List<ProjectHealthCheckEvidenceStageDto> Stages { get; init; } = [];

    /// <summary>
    /// The delivery forecast against the project's end, or null when it could not be produced — see
    /// <see cref="ForecastUnavailableReason"/>.
    /// </summary>
    public WorkItemForecastDto? Forecast { get; init; }

    /// <summary>Why <see cref="Forecast"/> is null; null when there is a forecast.</summary>
    public string? ForecastUnavailableReason { get; init; }

    /// <summary>
    /// Why there is no critical-path evidence. Critical-path analysis is not implemented, so this is always
    /// set; do not read its absence as "no critical tasks".
    /// </summary>
    public required string CriticalPathUnavailableReason { get; init; }

    /// <summary>The most recent health check, or null for a project never checked.</summary>
    public ProjectHealthCheckDetailsDto? LatestHealthCheck { get; init; }

    /// <summary>
    /// The project's activity since <see cref="LatestHealthCheck"/> was reported, newest first: schedule,
    /// status, role and detail changes. Tasks and stages are not included. For a project never checked, the
    /// most recent entries.
    /// </summary>
    public List<ActivityLogDto> ActivitySinceLatestCheck { get; init; } = [];

    /// <summary>True when there was more activity than was returned, so the list is not the whole story.</summary>
    public bool ActivityTruncated { get; init; }
}

/// <summary>
/// One stage of a project's plan, flagged when its end has passed and it is not finished.
/// </summary>
public sealed record ProjectHealthCheckEvidenceStageDto
{
    /// <summary>The stage's id.</summary>
    public Guid Id { get; init; }

    /// <summary>The stage's name.</summary>
    public required string Name { get; init; }

    /// <summary>The stage's status.</summary>
    public required SimpleNavigationDto Status { get; init; }

    /// <summary>The stage's position in the lifecycle.</summary>
    public int Order { get; init; }

    /// <summary>The stage's planned start.</summary>
    public LocalDate? Start { get; init; }

    /// <summary>The stage's planned end.</summary>
    public LocalDate? End { get; init; }

    /// <summary>The stage's progress, 0 to 100.</summary>
    public decimal Progress { get; init; }

    /// <summary>True when the stage's end is before <c>asOf</c> and it is neither completed nor canceled.</summary>
    public bool PastEndNotComplete { get; init; }
}
