using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Models;

namespace Wayd.Common.Domain.Models.Organizations;

/// <summary>
/// A Kanban team's reporting boundary: back-to-back periods of a whole number of weeks, one of which starts on
/// <see cref="Anchor"/>. The periods are worked out when read; nothing is stored per period, and a period has no
/// commitment or lifecycle.
/// </summary>
/// <remarks>
/// The anchor fixes the alignment, not the first period: periods run before it as well as after, so every period
/// starts on the anchor's day of the week.
/// </remarks>
public sealed class ReportingCadence : IEquatable<ReportingCadence>
{
    /// <summary>The shortest period, in weeks.</summary>
    public const int MinWeeks = 1;

    /// <summary>The longest period, in weeks.</summary>
    public const int MaxWeeks = 8;

    private ReportingCadence(int weeks, LocalDate anchor)
    {
        Weeks = weeks;
        Anchor = anchor;
    }

    /// <summary>The length of every period, in weeks.</summary>
    public int Weeks { get; }

    /// <summary>The first day of one of the periods, which aligns all the others.</summary>
    public LocalDate Anchor { get; }

    /// <summary>The length of every period, in days.</summary>
    public int Days => Weeks * 7;

    /// <summary>A cadence of <paramref name="weeks"/> aligned to <paramref name="anchor"/>.</summary>
    public static Result<ReportingCadence> Create(int weeks, LocalDate anchor)
    {
        if (weeks is < MinWeeks or > MaxWeeks)
            return Result.Failure<ReportingCadence>($"A cadence must be between {MinWeeks} and {MaxWeeks} weeks long.");

        return new ReportingCadence(weeks, anchor);
    }

    /// <summary>
    /// The cadence given as an optional length and anchor, or null when neither is given. Fails when only one is.
    /// </summary>
    public static Result<ReportingCadence?> CreateOptional(int? weeks, LocalDate? anchor) =>
        (weeks, anchor) switch
        {
            (null, null) => Result.Success<ReportingCadence?>(null),
            ({ } w, { } a) => Create(w, a).Map(c => (ReportingCadence?)c),
            _ => Result.Failure<ReportingCadence?>("A cadence needs both a length and an anchor date."),
        };

    /// <summary>The whole period that <paramref name="date"/> falls in, which may start before the anchor.</summary>
    public LocalDateRange PeriodContaining(LocalDate date)
    {
        var offset = Period.DaysBetween(Anchor, date);
        var index = (int)Math.Floor(offset / (double)Days);
        var start = Anchor.PlusDays(index * Days);
        return new LocalDateRange(start, start.PlusDays(Days - 1));
    }

    /// <summary>
    /// The whole periods that overlap <paramref name="start"/> to <paramref name="end"/>, inclusive, oldest first.
    /// The first and last may extend past the range.
    /// </summary>
    public IEnumerable<LocalDateRange> PeriodsOverlapping(LocalDate start, LocalDate end)
    {
        if (end < start)
            yield break;

        for (var period = PeriodContaining(start); period.Start <= end; period = new LocalDateRange(period.End.PlusDays(1), period.End.PlusDays(Days)))
            yield return period;
    }

    /// <summary>Whether <paramref name="other"/> has the same length and anchor, and so the same periods.</summary>
    public bool Equals(ReportingCadence? other) => other is not null && Weeks == other.Weeks && Anchor == other.Anchor;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ReportingCadence);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Weeks, Anchor);

    /// <summary>Whether two cadences have the same length and anchor; two nulls are equal.</summary>
    public static bool operator ==(ReportingCadence? left, ReportingCadence? right) => Equals(left, right);

    /// <summary>Whether two cadences differ in length or anchor.</summary>
    public static bool operator !=(ReportingCadence? left, ReportingCadence? right) => !Equals(left, right);
}
