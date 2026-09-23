using Ardalis.GuardClauses;

namespace Wayd.Work.Domain.Models.Forecasting;

/// <summary>
/// The outcome of a "when will it be done?" simulation: for each trial, the number of days
/// it took to finish the remaining work. Day 1 is the first simulated day.
/// </summary>
public sealed class CompletionForecast
{
    // Trial order is preserved (not sorted) so forecasts can be paired trial by trial, as
    // LatestOf and DependencyForecaster do. A trial that did not finish within the horizon
    // holds HorizonDays + 1.
    private readonly int[] _trialDays;
    private readonly Lazy<int[]> _sorted;

    internal CompletionForecast(int[] trialDays, int horizonDays)
    {
        _trialDays = trialDays;
        HorizonDays = horizonDays;
        _sorted = new Lazy<int[]>(() => _trialDays.Order().ToArray());
    }

    public int Trials => _trialDays.Length;

    internal int this[int trial] => _trialDays[trial];

    /// <summary>
    /// The most days a trial was simulated before it was abandoned as unfinished.
    /// </summary>
    public int HorizonDays { get; }

    /// <summary>
    /// The number of trials that did not finish within <see cref="HorizonDays"/>.
    /// </summary>
    public int TrialsBeyondHorizon => _trialDays.Count(d => d > HorizonDays);

    /// <summary>
    /// Days needed per trial, sorted ascending; trials beyond the horizon are omitted
    /// (see <see cref="TrialsBeyondHorizon"/>).
    /// </summary>
    public IReadOnlyList<int> FinishedTrialDays => _sorted.Value[..(Trials - TrialsBeyondHorizon)];

    /// <summary>
    /// The fewest days within which at least <paramref name="percent"/>% of trials finished,
    /// or null when that many trials did not finish within the horizon.
    /// </summary>
    public int? DaysAtConfidence(int percent)
    {
        Guard.Against.OutOfRange(percent, nameof(percent), 1, 100);

        var sorted = _sorted.Value;
        var index = (sorted.Length * percent + 99) / 100 - 1;
        var days = sorted[index];
        return days > HorizonDays ? null : days;
    }

    /// <summary>
    /// The share of trials (0 to 1) that finished within <paramref name="days"/> days. Zero or
    /// fewer days is only met by trials with nothing left to do.
    /// </summary>
    public double ShareFinishedWithin(int days) =>
        (double)_trialDays.Count(d => d <= days && d <= HorizonDays) / Trials;

    /// <summary>
    /// Combines forecasts for work that is done only when all of it is done, such as an epic
    /// spread across several teams: each trial takes the latest finish. Forecasts for the same
    /// team must come from one <see cref="MonteCarloForecaster.ForecastBacklogPositions"/>
    /// call, so that trial i is the same simulated run for each of them.
    /// </summary>
    public static CompletionForecast LatestOf(IReadOnlyCollection<CompletionForecast> forecasts)
    {
        Guard.Against.NullOrEmpty(forecasts);

        var first = forecasts.First();
        EnsureComparable(forecasts, nameof(forecasts));

        var latest = new int[first.Trials];
        foreach (var forecast in forecasts)
        {
            for (var i = 0; i < latest.Length; i++)
                latest[i] = Math.Max(latest[i], forecast._trialDays[i]);
        }

        return new CompletionForecast(latest, first.HorizonDays);
    }

    internal static void EnsureComparable(IEnumerable<CompletionForecast> forecasts, string paramName)
    {
        CompletionForecast? first = null;
        foreach (var forecast in forecasts)
        {
            first ??= forecast;
            if (forecast.Trials != first.Trials || forecast.HorizonDays != first.HorizonDays)
                throw new ArgumentException("Forecasts must share a trial count and horizon to be combined.", paramName);
        }
    }
}
