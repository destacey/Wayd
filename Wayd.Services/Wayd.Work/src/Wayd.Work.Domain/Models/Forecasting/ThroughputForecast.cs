using Ardalis.GuardClauses;

namespace Wayd.Work.Domain.Models.Forecasting;

/// <summary>
/// The outcome of a "how much will be done by then?" simulation: for each trial, the total
/// work finished over the simulated days.
/// </summary>
public sealed class ThroughputForecast
{
    private readonly double[] _sorted;

    internal ThroughputForecast(double[] trialTotals, int days)
    {
        _sorted = trialTotals.Order().ToArray();
        Days = days;
    }

    public int Trials => _sorted.Length;

    public int Days { get; }

    /// <summary>
    /// Work finished per trial, sorted ascending.
    /// </summary>
    public IReadOnlyList<double> TrialTotals => _sorted;

    /// <summary>
    /// The most work that at least <paramref name="percent"/>% of trials finished. Higher
    /// confidence gives a smaller amount — the reverse of <see cref="CompletionForecast"/>.
    /// </summary>
    public double AmountAtConfidence(int percent)
    {
        Guard.Against.OutOfRange(percent, nameof(percent), 1, 100);

        return _sorted[_sorted.Length * (100 - percent) / 100];
    }
}
