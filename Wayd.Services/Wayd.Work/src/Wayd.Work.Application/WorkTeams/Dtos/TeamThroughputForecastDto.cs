using Wayd.Common.Application.Dtos;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkTeams.Dtos;

/// <summary>
/// How many backlog work items a team will finish from <c>forecastStart</c> through
/// <c>targetDate</c>.
/// </summary>
public sealed record TeamThroughputForecastDto
{
    /// <summary>
    /// Whether a forecast could be made: Forecast, or Not Enough History when the team has not
    /// finished enough work recently to forecast from.
    /// </summary>
    public required SimpleNavigationDto Outcome { get; init; }

    public required ForecastTeamDto Team { get; init; }

    /// <summary>
    /// The first simulated day (UTC).
    /// </summary>
    public required LocalDate ForecastStart { get; init; }

    public required LocalDate TargetDate { get; init; }

    /// <summary>
    /// Days of history the team's throughput was sampled from.
    /// </summary>
    public int LookbackDays { get; init; }

    /// <summary>
    /// Whether active backlog items were counted ahead of proposed ones.
    /// </summary>
    public bool StartedWorkFirst { get; init; }

    /// <summary>
    /// Simulated days, <c>forecastStart</c> through <c>targetDate</c> inclusive.
    /// </summary>
    public int Days { get; init; }

    /// <summary>
    /// The team's open backlog work items today.
    /// </summary>
    public int BacklogWorkItems { get; init; }

    public int Trials { get; init; }

    public List<ThroughputPercentileDto> Percentiles { get; init; } = [];

    public List<ThroughputHistogramBucketDto> Histogram { get; init; } = [];
}
