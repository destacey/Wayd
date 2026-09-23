using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkTeams.Dtos;

public sealed record ThroughputPercentileDto
{
    /// <summary>
    /// The share of trials, as a whole percent, that finished at least <see cref="WorkItems"/>.
    /// </summary>
    public required int Confidence { get; init; }

    public required int WorkItems { get; init; }

    /// <summary>
    /// The backlog item that many items down, first-ranked first: work down to and including it
    /// is done at this confidence. Null when nothing is, or when the count runs past the backlog.
    /// </summary>
    public ForecastWorkItemDto? ThroughWorkItem { get; init; }
}
