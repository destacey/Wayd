using NodaTime;

namespace Wayd.Work.Domain.Models.SprintScope;

/// <summary>
/// A point on a sprint's ideal burn-down: the share of the committed work still expected to remain at
/// <paramref name="At"/>, from 1 at the commitment point to 0 at the effective end. A share rather than an
/// amount, so it serves every unit the sprint is measured in.
/// </summary>
public sealed record SprintIdealPoint(Instant At, double Remaining);

/// <summary>
/// A sprint's ideal burn-down, which its burn chart draws and its health is measured against: falling across
/// the days the team works and flat across the rest.
/// </summary>
public static class SprintIdealLine
{
    /// <summary>
    /// The ideal line: the share of the committed work left at the commitment point, the start of each day in
    /// the team's zone, and the effective end. Each day's span counts in proportion to its weight and to how
    /// much of the day falls in the window, so a window that starts or ends mid-day counts that day in part. A
    /// window with no working time at all falls back to counting every day, rather than drawing no line.
    /// </summary>
    public static List<SprintIdealPoint> Build(SprintScopeWindow window, SprintWorkingDays workingDays)
    {
        if (window.End <= window.Start)
            return [new SprintIdealPoint(window.Start, 1), new SprintIdealPoint(window.End, 0)];

        var zone = window.TimeZone;
        List<Instant> boundaries = [window.Start];
        for (var day = window.Start.InZone(zone).Date.PlusDays(1); ; day = day.PlusDays(1))
        {
            var startOfDay = day.AtStartOfDayInZone(zone).ToInstant();
            if (startOfDay >= window.End)
                break;
            boundaries.Add(startOfDay);
        }
        boundaries.Add(window.End);

        // The work in each span between boundaries: the part of its day it covers, times the day's weight.
        var spans = boundaries.Zip(boundaries.Skip(1), (from, to) =>
        {
            var day = from.InZone(zone).Date;
            var dayLength = day.PlusDays(1).AtStartOfDayInZone(zone).ToInstant() - day.AtStartOfDayInZone(zone).ToInstant();
            var share = (to - from) / dayLength;
            return (Weighted: share * workingDays.Weight(day), Unweighted: share);
        }).ToList();

        var useWeights = spans.Sum(s => s.Weighted) > 0;
        var work = spans.Select(s => useWeights ? s.Weighted : s.Unweighted).ToList();
        var total = work.Sum();

        var ideal = new List<SprintIdealPoint>(boundaries.Count) { new(window.Start, 1) };
        var done = 0.0;
        for (var i = 0; i < work.Count; i++)
        {
            done += work[i];
            // The last point is exactly zero, free of rounding in the sum.
            var remaining = i == work.Count - 1 ? 0 : Math.Max(0, 1 - done / total);
            ideal.Add(new SprintIdealPoint(boundaries[i + 1], remaining));
        }

        return ideal;
    }
}
