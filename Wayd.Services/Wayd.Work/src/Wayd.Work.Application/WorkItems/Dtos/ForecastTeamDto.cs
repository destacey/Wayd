using Wayd.Work.Application.WorkTeams.Dtos;

namespace Wayd.Work.Application.WorkItems.Dtos;

/// <summary>
/// A team whose throughput a forecast drew on, and the history it was sampled from: whole UTC
/// days, inclusive.
/// </summary>
public sealed record ForecastTeamDto
{
    public required WorkTeamNavigationDto Team { get; init; }
    public required LocalDate From { get; init; }
    public required LocalDate To { get; init; }
    public required int ItemsCompleted { get; init; }
}
