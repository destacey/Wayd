namespace Wayd.Work.Application.WorkItems.Dtos;

public sealed record ForecastDependencyInfluenceDto
{
    public required ForecastWorkItemDto Predecessor { get; init; }
    public required ForecastWorkItemDto Successor { get; init; }

    /// <summary>
    /// The share of trials (0 to 1) in which waiting on the predecessor made the successor
    /// finish later than its own backlog position allowed. Null when the successor cannot be
    /// forecast.
    /// </summary>
    public double? ShareOfTrialsSettingFinish { get; init; }
}
