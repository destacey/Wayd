using CSharpFunctionalExtensions;
using NodaTime;

namespace Wayd.Common.Domain.Kpis;

/// <summary>
/// The days a KPI value describes, both ends inclusive: a measurement's period or a baseline's window.
/// </summary>
/// <remarks>
/// Its own type rather than <c>LocalDateRange</c> so it can travel in an event payload and map as a complex type
/// without that class's private setters.
/// </remarks>
public sealed record KpiPeriod
{
    /// <exception cref="ArgumentException"><paramref name="end"/> is before <paramref name="start"/>.</exception>
    public KpiPeriod(LocalDate start, LocalDate end)
    {
        if (end < start)
            throw new ArgumentException(KpiErrors.PeriodEndsBeforeStart, nameof(end));

        Start = start;
        End = end;
    }

    /// <summary>Builds a period from caller input, failing rather than throwing when it ends before it starts.</summary>
    public static Result<KpiPeriod> Create(LocalDate start, LocalDate end) =>
        end < start
            ? Result.Failure<KpiPeriod>(KpiErrors.PeriodEndsBeforeStart)
            : new KpiPeriod(start, end);

    /// <summary>The first day of the period.</summary>
    public LocalDate Start { get; }

    /// <summary>The last day of the period, inclusive.</summary>
    public LocalDate End { get; }

    /// <summary>Whether the two periods share at least one day.</summary>
    public bool Overlaps(KpiPeriod other) => Start <= other.End && other.Start <= End;
}
