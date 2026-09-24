namespace Wayd.Work.Application.WorkItems.Forecasting;

/// <summary>
/// Per-request choices for a forecast. Defaults give the standard forecast.
/// </summary>
public sealed record ForecastOptions
{
    public const int DefaultLookbackDays = 90;
    public const int MinLookbackDays = 14;
    public const int MaxLookbackDays = 365;

    public static ForecastOptions Default { get; } = new();

    /// <summary>
    /// Whole UTC days of history, ending yesterday, that throughput is sampled from.
    /// </summary>
    public int LookbackDays { get; init; } = DefaultLookbackDays;

    /// <summary>
    /// Forecast as if no work waited on its predecessors — a what-if showing what dependencies cost.
    /// </summary>
    public bool IgnoreDependencies { get; init; }

    /// <summary>
    /// Count active backlog items ahead of proposed ones, each in rank order: a team usually
    /// finishes what it has started before starting more. Off, the backlog is in rank order alone.
    /// </summary>
    public bool StartedWorkFirst { get; init; } = true;
}

public sealed class ForecastOptionsValidator : AbstractValidator<ForecastOptions>
{
    public ForecastOptionsValidator()
    {
        RuleFor(o => o.LookbackDays)
            .InclusiveBetween(ForecastOptions.MinLookbackDays, ForecastOptions.MaxLookbackDays)
            .WithMessage($"The history window must be between {ForecastOptions.MinLookbackDays} and {ForecastOptions.MaxLookbackDays} days.");
    }
}
