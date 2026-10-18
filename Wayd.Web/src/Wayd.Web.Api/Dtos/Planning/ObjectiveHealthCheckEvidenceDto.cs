using Wayd.Common.Application.Activities.Dtos;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Web.Api.Dtos.Planning;

/// <summary>
/// Everything needed to recommend a health check for one planning interval objective, gathered in
/// one read: the objective and its interval, its schedule, linked work and its trend, the delivery
/// forecast, its health check history, what changed since the last check, and its team's risks.
/// </summary>
public sealed record ObjectiveHealthCheckEvidenceDto
{
    /// <summary>The planning interval the objective belongs to.</summary>
    public required PlanningIntervalDetailsDto PlanningInterval { get; init; }

    /// <summary>The objective: status, progress, dates, team and current health check.</summary>
    public required PlanningIntervalObjectiveDetailsDto Objective { get; init; }

    /// <summary>The objective's effective window and how much of it has passed.</summary>
    public required ObjectiveScheduleEvidenceDto Schedule { get; init; }

    /// <summary>
    /// The linked work items, by backlog stack rank, with their progress rollup. Null when the
    /// objective has no linked work.
    /// </summary>
    public WorkItemsSummaryDto? WorkItems { get; init; }

    /// <summary>
    /// Daily progress of the linked work from the interval's start to the earlier of today and the
    /// interval's end.
    /// </summary>
    public IReadOnlyList<WorkItemProgressDailyRollupDto> WorkItemMetrics { get; init; } = [];

    /// <summary>
    /// Monte Carlo forecast of the linked work against `schedule.targetDate`.
    /// Null when `forecastUnavailableReason` says why.
    /// </summary>
    public WorkItemForecastDto? Forecast { get; init; }

    /// <summary>Why `forecast` is null, such as delivery forecasting being turned off.</summary>
    public string? ForecastUnavailableReason { get; init; }

    /// <summary>The objective's health checks, newest first. The first is the last check.</summary>
    public IReadOnlyList<PlanningIntervalObjectiveHealthCheckDetailsDto> HealthChecks { get; init; } = [];

    /// <summary>What changed on the objective since the last check.</summary>
    public required ObjectiveChangesSinceLastCheckDto SinceLastCheck { get; init; }

    /// <summary>
    /// The objective's team's risks in this interval — open ones only unless closed risks were asked
    /// for. Risks link to a team, not an objective, so these are the risks the team carries.
    /// </summary>
    public IReadOnlyList<RiskListDto> TeamRisks { get; init; } = [];
}

/// <summary>An objective's effective start and target, and the share of that window already past.</summary>
public sealed record ObjectiveScheduleEvidenceDto
{
    /// <summary>The objective's start date, or the interval's start when it has none.</summary>
    public required LocalDate StartDate { get; init; }

    /// <summary>True when `startDate` is the interval's start because the objective has none.</summary>
    public required bool StartDateDefaulted { get; init; }

    /// <summary>The objective's target date, or the interval's end when it has none.</summary>
    public required LocalDate TargetDate { get; init; }

    /// <summary>True when `targetDate` is the interval's end because the objective has none.</summary>
    public required bool TargetDateDefaulted { get; init; }

    /// <summary>Today's date, against which `elapsedShare` is measured.</summary>
    public required LocalDate Today { get; init; }

    /// <summary>
    /// How much of the window from `startDate` to `targetDate` has passed,
    /// from 0 (not started) to 1 (target reached or passed).
    /// </summary>
    public required double ElapsedShare { get; init; }
}

/// <summary>The objective's recorded changes since its last health check.</summary>
public sealed record ObjectiveChangesSinceLastCheckDto
{
    /// <summary>When the last check was reported. Null when the objective has never been checked.</summary>
    public Instant? LastCheckReportedOn { get; init; }

    /// <summary>
    /// The objective's progress (0 to 100) when it was last checked. Null when it has never been
    /// checked or the history read did not reach back that far.
    /// </summary>
    public double? ProgressAtLastCheck { get; init; }

    /// <summary>
    /// Progress points gained (or, negative, lost) since the last check. Null when
    /// `progressAtLastCheck` is.
    /// </summary>
    public double? ProgressChange { get; init; }

    /// <summary>
    /// The objective's activity since the last check, newest first. With no check, the most recent
    /// entries.
    /// </summary>
    public IReadOnlyList<ActivityLogDto> Activity { get; init; } = [];

    /// <summary>
    /// True when there is more activity in that period than is listed; page
    /// PlanningIntervals_GetObjectiveActivities for the rest.
    /// </summary>
    public bool ActivityTruncated { get; init; }
}
