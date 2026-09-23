namespace Wayd.Work.Application.WorkItems.Dtos;

public sealed record ForecastPercentileDto
{
    /// <summary>
    /// The share of trials, as a whole percent, that finished on or before <see cref="Date"/>.
    /// </summary>
    public required int Confidence { get; init; }

    /// <summary>
    /// Null when that many trials did not finish within the forecast horizon.
    /// </summary>
    public LocalDate? Date { get; init; }
}
