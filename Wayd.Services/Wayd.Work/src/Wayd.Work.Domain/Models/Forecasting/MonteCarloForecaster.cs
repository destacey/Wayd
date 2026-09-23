using Ardalis.GuardClauses;

namespace Wayd.Work.Domain.Models.Forecasting;

/// <summary>
/// Forecasts delivery by resampling a team's historical daily throughput: each simulated day
/// draws one day at random, with replacement, from the sample.
/// </summary>
public static class MonteCarloForecaster
{
    public const int DefaultTrials = 10_000;

    /// <summary>
    /// Bounds the work in a "when" simulation. A trial still unfinished after this many days is
    /// reported as beyond the horizon — without a bound, a near-zero sample against a large
    /// backlog would run for millions of draws per trial.
    /// </summary>
    public const int DefaultHorizonDays = 730;

    /// <summary>
    /// How many days it takes to finish <paramref name="remaining"/> work.
    /// </summary>
    public static CompletionForecast ForecastCompletion(
        ThroughputSample sample,
        double remaining,
        Random random,
        int trials = DefaultTrials,
        int horizonDays = DefaultHorizonDays)
        => ForecastBacklogPositions(sample, [remaining], random, trials, horizonDays)[0];

    /// <summary>
    /// When each of several items in one team's backlog finishes. An item's position is the
    /// work that must be finished for it to be done: its own and everything ranked ahead of it.
    /// </summary>
    /// <remarks>
    /// Every position is read from the same simulated run of the team's days in each trial.
    /// Forecasting the positions separately would treat one team as several working in
    /// parallel, and pairing them trial by trial (<see cref="CompletionForecast.LatestOf"/>,
    /// <see cref="DependencyForecaster"/>) would then be wrong.
    /// </remarks>
    /// <returns>One forecast per position, in the order given.</returns>
    public static IReadOnlyList<CompletionForecast> ForecastBacklogPositions(
        ThroughputSample sample,
        IReadOnlyList<double> positions,
        Random random,
        int trials = DefaultTrials,
        int horizonDays = DefaultHorizonDays)
    {
        Guard.Against.Null(sample);
        Guard.Against.Null(positions);
        Guard.Against.Null(random);
        Guard.Against.NegativeOrZero(trials);
        Guard.Against.NegativeOrZero(horizonDays);
        if (positions.Any(p => p < 0 || !double.IsFinite(p)))
            throw new ArgumentOutOfRangeException(nameof(positions), "Backlog positions must be finite and not negative.");

        var trialDays = new int[positions.Count][];
        for (var i = 0; i < trialDays.Length; i++)
            trialDays[i] = new int[trials];

        var byPosition = Enumerable.Range(0, positions.Count).OrderBy(i => positions[i]).ToArray();
        var beyondHorizon = horizonDays + 1;

        for (var trial = 0; trial < trials; trial++)
        {
            var next = 0;
            var done = 0d;
            var day = 0;

            while (next < byPosition.Length && positions[byPosition[next]] <= 0)
                trialDays[byPosition[next++]][trial] = 0;

            while (next < byPosition.Length && sample.HasThroughput && day <= horizonDays)
            {
                done += sample[random.Next(sample.Days)];
                day++;
                while (next < byPosition.Length && done >= positions[byPosition[next]])
                    trialDays[byPosition[next++]][trial] = day;
            }

            while (next < byPosition.Length)
                trialDays[byPosition[next++]][trial] = beyondHorizon;
        }

        return [.. trialDays.Select(days => new CompletionForecast(days, horizonDays))];
    }

    /// <summary>
    /// How much work is finished over the next <paramref name="days"/> days.
    /// </summary>
    public static ThroughputForecast ForecastThroughput(
        ThroughputSample sample,
        int days,
        Random random,
        int trials = DefaultTrials)
    {
        Guard.Against.Null(sample);
        Guard.Against.Null(random);
        Guard.Against.Negative(days);
        Guard.Against.NegativeOrZero(trials);

        var totals = new double[trials];
        for (var trial = 0; trial < trials; trial++)
        {
            var total = 0d;
            for (var day = 0; day < days; day++)
                total += sample[random.Next(sample.Days)];
            totals[trial] = total;
        }

        return new ThroughputForecast(totals, days);
    }
}
