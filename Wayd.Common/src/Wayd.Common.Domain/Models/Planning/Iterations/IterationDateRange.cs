using CSharpFunctionalExtensions;
using Wayd.Common.Interfaces;
using NodaTime;

namespace Wayd.Common.Domain.Models.Planning.Iterations;

/// <summary>
/// An iteration's planned dates: calendar days, with <see cref="End"/> the last day of the iteration rather
/// than the moment after it. No zone is implied; a consumer that needs instants applies one.
/// </summary>
public sealed class IterationDateRange : ValueObject, IDateRange<LocalDate?>
{
    public IterationDateRange(LocalDate? start, LocalDate? end)
    {
        Start = start;
        End = end;

        if (Start.HasValue && End.HasValue && End < Start)
        {
            throw new ArgumentException("The start date must be on or before the end date.", nameof(IterationDateRange));
        }
    }

    /// <summary>
    /// Gets the first planned day, if available.
    /// </summary>
    public LocalDate? Start { get; private set; }

    /// <summary>
    /// Gets the last planned day, if available. The iteration includes the whole of this day.
    /// </summary>
    public LocalDate? End { get; private set; }

    /// <summary>
    /// Gets the effective start date. If <see cref="Start"/> is null, this will return <see cref="LocalDate.MinIsoValue"/>.
    /// </summary>
    public LocalDate EffectiveStart => Start ?? LocalDate.MinIsoValue;

    /// <summary>
    /// Gets the effective end date. If <see cref="End"/> is null, this will return <see cref="LocalDate.MaxIsoValue"/>.
    /// </summary>
    public LocalDate EffectiveEnd => End ?? LocalDate.MaxIsoValue;

    /// <summary>
    /// Gets the number of days in the range, counting both the first and the last day.
    /// </summary>
    public int Days => Period.DaysBetween(EffectiveStart, EffectiveEnd) + 1;

    /// <summary>
    /// Determines whether the range includes the specified date.
    /// </summary>
    /// <param name="value">If null, it is treated as <see cref="LocalDate.MinIsoValue"/>.</param>
    public bool Includes(LocalDate? value)
    {
        var date = value ?? LocalDate.MinIsoValue;
        return EffectiveStart <= date && date <= EffectiveEnd;
    }

    /// <summary>
    /// Determines whether the range includes the specified range.
    /// </summary>
    public bool Includes(IterationDateRange range)
    {
        return EffectiveStart <= range.EffectiveStart && range.EffectiveEnd <= EffectiveEnd;
    }

    /// <summary>
    /// Determines whether the range overlaps the specified range.
    /// </summary>
    public bool Overlaps(IterationDateRange range)
    {
        return EffectiveStart <= range.EffectiveEnd && range.EffectiveStart <= EffectiveEnd;
    }

    /// <summary>
    /// Whether the last planned day is before <paramref name="date"/>.
    /// </summary>
    public bool IsPastOn(LocalDate date)
    {
        return EffectiveEnd < date;
    }

    /// <summary>
    /// Whether <paramref name="date"/> is one of the planned days.
    /// </summary>
    public bool IsActiveOn(LocalDate date)
    {
        return Includes(date);
    }

    /// <summary>
    /// Whether the first planned day is after <paramref name="date"/>.
    /// </summary>
    public bool IsFutureOn(LocalDate date)
    {
        return date < EffectiveStart;
    }

    protected override IEnumerable<IComparable> GetEqualityComponents()
    {
        yield return EffectiveStart;
        yield return EffectiveEnd;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="IterationDateRange"/> class with the specified start and end dates.
    /// </summary>
    /// <param name="start">The first planned day, or <see langword="null"/> when there is none.</param>
    /// <param name="end">The last planned day, or <see langword="null"/> when there is none.</param>
    public static IterationDateRange Create(LocalDate? start, LocalDate? end)
        => new(start, end);
}
