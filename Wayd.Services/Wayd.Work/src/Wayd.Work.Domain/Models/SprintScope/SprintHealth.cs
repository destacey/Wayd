using NodaTime;
using Wayd.Common.Domain.Enums.Work;

namespace Wayd.Work.Domain.Models.SprintScope;

/// <summary>
/// How far a running sprint is behind: the share of its work done, against the share expected done by now. The
/// sprint card's health indicator (<c>calculateSprintHealth</c> in the client) applies the same rules, so the two
/// never disagree.
/// </summary>
/// <param name="ExpectedShare">The share of the work expected done by now, from 0 to 1.</param>
/// <param name="DoneShare">Work done as a share of the work measured against; above 1 when more than committed was delivered.</param>
/// <param name="Status">On Track within 10 points of the ideal line, Watch up to 25 behind, Needs Attention beyond.</param>
public sealed record SprintHealth(double ExpectedShare, double DoneShare, AttentionStatus Status)
{
    /// <summary>How many percentage points behind the ideal line a sprint may fall and stay on track.</summary>
    public const double OnTrackBehindPercent = 10;

    /// <summary>How many percentage points behind the ideal line a sprint may fall before it needs attention.</summary>
    public const double WatchBehindPercent = 25;

    /// <summary>
    /// Measures the sprint at <paramref name="now"/>. Null when there is nothing to measure against: before the
    /// commitment point, at or after the effective end, or with nothing committed.
    /// </summary>
    /// <param name="window">The sprint's commitment point and effective end.</param>
    /// <param name="ideal">The sprint's ideal burn-down (<see cref="SprintIdealLine"/>); read evenly from start to end when it does not span <paramref name="now"/>.</param>
    /// <param name="committed">The work committed at the commitment point.</param>
    /// <param name="delivered">The work completed so far, in the same measure.</param>
    public static SprintHealth? Measure(SprintScopeWindow window, IReadOnlyList<SprintIdealPoint> ideal, double committed, double delivered, Instant now)
    {
        if (now < window.Start || now >= window.End || committed <= 0)
            return null;

        var expected = ExpectedDoneAt(ideal, now)
            ?? (now - window.Start).TotalTicks / (window.End - window.Start).TotalTicks;
        return Of(expected, delivered / committed);
    }

    /// <summary>
    /// Measures a sprint with no commitment to measure against — before its commitment point, with its history not
    /// yet read in full, or with nothing committed — by its work done against the share of its days gone, counting
    /// calendar days from <paramref name="firstDay"/> to <paramref name="lastDay"/>. Null before its first day,
    /// after its last, with no work, or with no days between the two.
    /// </summary>
    /// <param name="firstDay">The sprint's first active day, in its team's zone.</param>
    /// <param name="lastDay">The sprint's last active day, in its team's zone.</param>
    /// <param name="today">Today, in the same zone.</param>
    /// <param name="total">The work measured against.</param>
    /// <param name="completed">The part of <paramref name="total"/> done, in the same measure.</param>
    public static SprintHealth? MeasureOverDays(LocalDate firstDay, LocalDate lastDay, LocalDate today, double total, double completed)
    {
        var daysGone = Period.DaysBetween(firstDay, today);
        var days = Period.DaysBetween(firstDay, lastDay);
        if (daysGone <= 0 || today > lastDay || total <= 0 || days <= 0)
            return null;

        return Of((double)Math.Min(daysGone, days) / days, completed / total);
    }

    private static SprintHealth Of(double expected, double done)
    {
        var behindPercent = (expected - done) * 100;
        var status = behindPercent <= OnTrackBehindPercent
            ? AttentionStatus.OnTrack
            : behindPercent <= WatchBehindPercent
                ? AttentionStatus.Watch
                : AttentionStatus.NeedsAttention;

        return new SprintHealth(expected, done, status);
    }

    /// <summary>The share the ideal line expects done at <paramref name="now"/>, read between the points either side.</summary>
    private static double? ExpectedDoneAt(IReadOnlyList<SprintIdealPoint> ideal, Instant now)
    {
        for (var i = 1; i < ideal.Count; i++)
        {
            var (from, to) = (ideal[i - 1], ideal[i]);
            if (now < from.At || now > to.At)
                continue;

            var t = to.At > from.At ? (now - from.At).TotalTicks / (to.At - from.At).TotalTicks : 1;
            return 1 - (from.Remaining + (to.Remaining - from.Remaining) * t);
        }

        return null;
    }
}
