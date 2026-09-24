using Wayd.Common.Application.Dtos;

namespace Wayd.Work.Application.WorkItems.Dtos;

public sealed record WorkItemForecastDto
{
    /// <summary>
    /// A <see cref="WorkItemForecastOutcome"/>. Only <see cref="WorkItemForecastOutcome.Forecast"/>
    /// fills the percentiles and histogram.
    /// </summary>
    public required SimpleNavigationDto Outcome { get; init; }

    /// <summary>
    /// The first simulated day (UTC): a trial that finishes on its first day finishes on this date.
    /// </summary>
    public required LocalDate ForecastStart { get; init; }

    /// <summary>
    /// Days of history the teams' throughput was sampled from.
    /// </summary>
    public int LookbackDays { get; init; }

    /// <summary>
    /// Whether the forecast ignored dependencies, as a what-if.
    /// </summary>
    public bool IgnoreDependencies { get; init; }

    /// <summary>
    /// Whether active backlog items were counted ahead of proposed ones.
    /// </summary>
    public bool StartedWorkFirst { get; init; }

    /// <summary>
    /// The 1-based position in its team's backlog, when the forecast is for a single open backlog
    /// work item.
    /// </summary>
    public int? BacklogPosition { get; init; }

    /// <summary>
    /// The open backlog work items the forecast is for. A backlog work item counts itself; a
    /// portfolio work item counts its open backlog descendants.
    /// </summary>
    public int RemainingWorkItems { get; init; }

    /// <summary>
    /// Remaining work items left out because they could not be forecast (see <see cref="Issues"/>
    /// for why). A forecast with exclusions is a lower bound.
    /// </summary>
    public List<ForecastWorkItemDto> ExcludedWorkItems { get; init; } = [];

    /// <summary>
    /// Every team whose throughput the forecast drew on, including predecessors' teams.
    /// </summary>
    public List<ForecastTeamDto> Teams { get; init; } = [];

    /// <summary>
    /// The date the work is due, when there is one: an objective's target date or its planning
    /// interval's end, or a project's planned end.
    /// </summary>
    public LocalDate? TargetDate { get; init; }

    /// <summary>
    /// The share of trials (0 to 1) that finished on or before <see cref="TargetDate"/>. Null
    /// without a target date or a forecast.
    /// </summary>
    public double? ChanceOfFinishingByTargetDate { get; init; }

    public int Trials { get; init; }

    public int TrialsBeyondHorizon { get; init; }

    public List<ForecastPercentileDto> Percentiles { get; init; } = [];

    public List<ForecastHistogramBucketDto> Histogram { get; init; } = [];

    public List<ForecastDependencyInfluenceDto> Dependencies { get; init; } = [];

    /// <summary>
    /// Dependencies left out of the forecast, each with its <see cref="ForecastDependencyLinkDto.Reason"/>:
    /// it closed a cycle, or its predecessor was removed.
    /// </summary>
    public List<ForecastDependencyLinkDto> IgnoredDependencies { get; init; } = [];

    /// <summary>
    /// Why the work item, or something it depends on, could not be forecast or is only a lower bound.
    /// </summary>
    public List<ForecastIssueDto> Issues { get; init; } = [];
}
