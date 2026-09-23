using Ardalis.GuardClauses;
using NodaTime;

namespace Wayd.Work.Domain.Models.Forecasting;

/// <summary>
/// A team's historical throughput: the amount of work finished on each calendar day of a
/// lookback window. The values are unit-agnostic — an item count today, story points later.
/// </summary>
public sealed class ThroughputSample
{
    private readonly double[] _daily;

    private ThroughputSample(double[] daily)
    {
        _daily = daily;
        Total = daily.Sum();
    }

    /// <summary>
    /// Throughput per calendar day, oldest first. Days with nothing finished are present as 0.
    /// </summary>
    public IReadOnlyList<double> DailyThroughput => _daily;

    public int Days => _daily.Length;

    public double Total { get; }

    public bool HasThroughput => Total > 0;

    internal double this[int day] => _daily[day];

    /// <summary>
    /// Builds a sample from the dates items were finished, counting one per item. Every day from
    /// <paramref name="from"/> to <paramref name="to"/> (inclusive) is in the sample, including
    /// days with no completions — dropping them would overstate the team's pace.
    /// </summary>
    public static ThroughputSample FromCompletions(IEnumerable<LocalDate> completedOn, LocalDate from, LocalDate to)
    {
        Guard.Against.Null(completedOn);
        if (to < from)
            throw new ArgumentOutOfRangeException(nameof(to), to, "The window must not end before it starts.");

        var daily = new double[Period.DaysBetween(from, to) + 1];
        foreach (var date in completedOn)
        {
            if (date < from || date > to)
                throw new ArgumentOutOfRangeException(nameof(completedOn), date, $"Completion falls outside the window {from} to {to}.");

            daily[Period.DaysBetween(from, date)]++;
        }

        return new ThroughputSample(daily);
    }

    public static ThroughputSample FromDailyThroughput(IEnumerable<double> dailyThroughput)
    {
        Guard.Against.Null(dailyThroughput);

        var daily = dailyThroughput.ToArray();
        if (daily.Length == 0)
            throw new ArgumentException("A sample needs at least one day.", nameof(dailyThroughput));
        if (daily.Any(d => d < 0 || !double.IsFinite(d)))
            throw new ArgumentOutOfRangeException(nameof(dailyThroughput), "Daily throughput must be finite and not negative.");

        return new ThroughputSample(daily);
    }
}
