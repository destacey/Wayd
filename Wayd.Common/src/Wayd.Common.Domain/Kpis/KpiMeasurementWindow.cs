using CSharpFunctionalExtensions;
using NodaTime;

namespace Wayd.Common.Domain.Kpis;

/// <summary>
/// How a KPI's measurements are windowed: whether each value gives its own period, or the period follows from a rule
/// and the day or moment the value is taken.
/// </summary>
/// <remarks>
/// Part of the KPI, not of each measurement, so a value is recorded once — as of a day or a moment — and its period is
/// worked out here. The worked-out period is stored on the measurement, so nothing that reads measurements needs this
/// rule. It cannot change once the KPI has measurements: stored periods would no longer mean what the rule says.
/// </remarks>
/// <param name="Kind">How the window is formed.</param>
/// <param name="AnchorDate">The first day every measurement covers, for <see cref="KpiWindowKind.Cumulative"/> only.</param>
/// <param name="RollingCount">How many units each measurement covers, for <see cref="KpiWindowKind.Rolling"/> only.</param>
/// <param name="RollingUnit">What the rolling count counts, for <see cref="KpiWindowKind.Rolling"/> only.</param>
public sealed record KpiMeasurementWindow(KpiWindowKind Kind, LocalDate? AnchorDate, int? RollingCount, KpiRollingUnit? RollingUnit)
{
    /// <summary>The longest rolling window, in days: ten years, well beyond any real KPI.</summary>
    public const int MaxRollingDays = 3660;

    /// <summary>Each measurement gives its own period, and periods do not overlap.</summary>
    public static KpiMeasurementWindow PerPeriod { get; } = new(KpiWindowKind.PerPeriod, null, null, null);

    /// <summary>Each measurement is the state at one moment.</summary>
    public static KpiMeasurementWindow PointInTime { get; } = new(KpiWindowKind.PointInTime, null, null, null);

    /// <summary>Each measurement covers the anchor date to the day it is taken.</summary>
    public static KpiMeasurementWindow Cumulative(LocalDate anchorDate) => new(KpiWindowKind.Cumulative, anchorDate, null, null);

    /// <summary>Each measurement covers the given number of units ending on the day it is taken.</summary>
    public static KpiMeasurementWindow Rolling(int count, KpiRollingUnit unit) => new(KpiWindowKind.Rolling, null, count, unit);

    /// <summary>Why this window cannot be held, or null when it can.</summary>
    public string? Validate()
    {
        if (!Enum.IsDefined(Kind))
            return $"Unsupported measurement window: {Kind}.";

        if (Kind == KpiWindowKind.Cumulative)
        {
            if (AnchorDate is null)
                return KpiErrors.CumulativeAnchorRequired;
        }
        else if (AnchorDate is not null)
        {
            return KpiErrors.AnchorOnlyForCumulative;
        }

        if (Kind == KpiWindowKind.Rolling)
        {
            if (RollingCount is not { } count || RollingUnit is not { } unit)
                return KpiErrors.RollingLengthRequired;
            if (!Enum.IsDefined(unit))
                return $"Unsupported rolling unit: {unit}.";
            if (count < 1 || (unit == KpiRollingUnit.Days && count > MaxRollingDays))
                return KpiErrors.RollingCountOutOfRange;
        }
        else if (RollingCount is not null || RollingUnit is not null)
        {
            return KpiErrors.RollingLengthOnlyForRolling;
        }

        return null;
    }

    /// <summary>
    /// The period a value taken at the given time covers under this window, or why the time does not fit it.
    /// </summary>
    /// <remarks>
    /// A point in time covers the day its moment falls on in <paramref name="zone"/> — the system time zone — so the
    /// period orders and charts it beside other measurements, while the moment itself tells two readings on one day
    /// apart. The day is stored as worked out then; a later change of zone does not move it.
    /// </remarks>
    /// <param name="time">When the value was taken, as the caller gave it.</param>
    /// <param name="zone">The zone a moment's calendar day is read in.</param>
    public Result<KpiPeriod> PeriodFor(KpiMeasurementTime time, DateTimeZone zone)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(zone);

        switch (Kind)
        {
            case KpiWindowKind.PerPeriod:
                return time is { Period: { } period, AsOf: null, At: null }
                    ? period
                    : Result.Failure<KpiPeriod>(KpiErrors.PeriodRequired);

            case KpiWindowKind.Cumulative:
                if (time is not { AsOf: { } cumulativeAsOf, Period: null, At: null })
                    return Result.Failure<KpiPeriod>(KpiErrors.AsOfDateRequired);
                return cumulativeAsOf < AnchorDate!.Value
                    ? Result.Failure<KpiPeriod>(KpiErrors.AsOfBeforeAnchor)
                    : new KpiPeriod(AnchorDate.Value, cumulativeAsOf);

            case KpiWindowKind.Rolling:
                if (time is not { AsOf: { } rollingAsOf, Period: null, At: null })
                    return Result.Failure<KpiPeriod>(KpiErrors.AsOfDateRequired);
                return new KpiPeriod(rollingAsOf.PlusDays(-(RollingCount!.Value - 1)), rollingAsOf);

            case KpiWindowKind.PointInTime:
                if (time is not { At: { } at, Period: null, AsOf: null })
                    return Result.Failure<KpiPeriod>(KpiErrors.MomentRequired);
                var day = at.InZone(zone).Date;
                return new KpiPeriod(day, day);

            default:
                throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unsupported measurement window.");
        }
    }
}

/// <summary>
/// When a value was taken, as a caller gives it: the period it describes, the day it was taken as of, or the moment
/// it describes. Which one a KPI takes is its <see cref="KpiMeasurementWindow"/>'s to say.
/// </summary>
/// <param name="Period">The days the value describes, for a per-period KPI.</param>
/// <param name="AsOf">The last day the value covers, for a cumulative or rolling KPI.</param>
/// <param name="At">The moment the value describes, for a point-in-time KPI.</param>
public sealed record KpiMeasurementTime(KpiPeriod? Period, LocalDate? AsOf, Instant? At)
{
    /// <summary>A value for the given days.</summary>
    public static KpiMeasurementTime ForPeriod(KpiPeriod period) => new(period, null, null);

    /// <summary>A value taken as of the given day.</summary>
    public static KpiMeasurementTime AsOfDate(LocalDate asOf) => new(null, asOf, null);

    /// <summary>A value describing the given moment.</summary>
    public static KpiMeasurementTime AtMoment(Instant at) => new(null, null, at);
}
