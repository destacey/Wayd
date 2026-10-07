using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Work;

namespace Wayd.Work.Domain.Models.SprintScope;

/// <summary>
/// One reading of a sprint's burn: its scope and the part of it completed at <paramref name="At"/>, as item
/// counts and as estimates in the sprint's sizing method.
/// </summary>
/// <param name="Day">The team's calendar day the reading closes, in the sprint's zone.</param>
public sealed record SprintBurnPoint(Instant At, LocalDate Day, SprintScopeMeasure Scope, SprintScopeMeasure Completed);

/// <summary>
/// A sprint's burn-up and burn-down, worked out from work item history: a reading at the commitment point, at
/// the end of each of the sprint's days in the team's zone, and at the effective end — or now, for a sprint
/// that has not ended. Worked out when read and never stored.
/// </summary>
/// <remarks>
/// Counts the work the scope report counts, so the two agree at the commitment point and at the end. An item
/// is in scope from when it was committed or added, while it is in the sprint. Once it has left for good it
/// stays in scope if it left completed — the team did that work — or carried over, and drops out if it was
/// descoped; leaving and coming back is a gap. It counts as completed while in a Done- or Removed-category
/// status, so a reopened item counts as remaining again. Its estimate is the one it had at each reading.
/// </remarks>
public sealed class SprintBurn
{
    private SprintBurn(SprintScopeWindow window, SizingMethod sizingMethod, SprintScopeMeasure committed, List<SprintBurnPoint> points)
    {
        Window = window;
        SizingMethod = sizingMethod;
        Committed = committed;
        Points = points;
    }

    /// <summary>The instants the burn runs between.</summary>
    public SprintScopeWindow Window { get; }

    /// <summary>The estimate the burn is measured in.</summary>
    public SizingMethod SizingMethod { get; }

    /// <summary>The work committed at the commitment point, where the ideal burn-down line starts.</summary>
    public SprintScopeMeasure Committed { get; }

    /// <summary>The readings, in time order; empty before the commitment point.</summary>
    public IReadOnlyList<SprintBurnPoint> Points { get; }

    /// <inheritdoc cref="SprintScopeReport.Build"/>
    public static SprintBurn Build(SprintScopeWindow window, SizingMethod sizingMethod, IEnumerable<SprintScopePeriod> periods, Instant now)
    {
        var assessed = SprintScopeReport.Assess(window, sizingMethod, periods, now);
        var committed = SprintScopeTotals.Of([.. assessed.Select(a => a.Item)]).Committed;
        if (now <= window.Start)
            return new SprintBurn(window, sizingMethod, committed, []);

        var asOf = now < window.End ? now : window.End;
        var points = ReadingTimes(window, asOf)
            .Select(at => Reading(window, sizingMethod, assessed, at))
            .ToList();

        return new SprintBurn(window, sizingMethod, committed, points);
    }

    /// <summary>
    /// The commitment point, the end of each day in the team's zone after it, and <paramref name="asOf"/>: the
    /// effective end, or now.
    /// </summary>
    private static List<Instant> ReadingTimes(SprintScopeWindow window, Instant asOf)
    {
        var zone = window.TimeZone;
        List<Instant> times = [window.Start];

        for (var day = window.Start.InZone(zone).Date.PlusDays(1); ; day = day.PlusDays(1))
        {
            var endOfDay = day.AtStartOfDayInZone(zone).ToInstant();
            if (endOfDay >= asOf)
                break;
            times.Add(endOfDay);
        }

        times.Add(asOf);
        return times;
    }

    private static SprintBurnPoint Reading(SprintScopeWindow window, SizingMethod sizingMethod, List<SprintScopeReport.AssessedItem> assessed, Instant at)
    {
        int scopeCount = 0, completedCount = 0;
        double scopeEstimate = 0, completedEstimate = 0;

        foreach (var item in assessed)
        {
            if (StateAt(window, sizingMethod, item, at) is not { } state)
                continue;

            var (completed, estimate) = state;

            scopeCount++;
            scopeEstimate += estimate ?? 0;
            if (completed)
            {
                completedCount++;
                completedEstimate += estimate ?? 0;
            }
        }

        // The day a reading closes: an end of day at midnight belongs to the day before it.
        var day = (at == window.Start ? at : at.Minus(Duration.Epsilon)).InZone(window.TimeZone).Date;

        return new SprintBurnPoint(
            at,
            day,
            new SprintScopeMeasure(scopeCount, scopeEstimate),
            new SprintScopeMeasure(completedCount, completedEstimate));
    }

    /// <summary>Whether the item is in scope at <paramref name="at"/>, and if so whether completed and its estimate.</summary>
    private static (bool Completed, double? Estimate)? StateAt(SprintScopeWindow window, SizingMethod sizingMethod, SprintScopeReport.AssessedItem assessed, Instant at)
    {
        var item = assessed.Item;
        var enteredAt = item.Entry == SprintScopeEntry.Committed ? window.Start : assessed.InSprint[0].ValidFrom;
        if (at < enteredAt)
            return null;

        var period = assessed.InSprint.FirstOrDefault(p => p.Covers(at));
        if (period is not null)
            return (IsFinished(period.StatusCategory), WorkItemEstimate.Of(sizingMethod, period.StoryPoints, period.Effort, period.Size));

        if (item.RemovedAt is not { } removedAt || at < removedAt)
            return null;

        return item.Outcome switch
        {
            SprintScopeOutcome.Completed or SprintScopeOutcome.Removed => (true, item.OutcomeEstimate),
            SprintScopeOutcome.CarriedOver => (false, item.OutcomeEstimate),
            _ => null,
        };
    }

    private static bool IsFinished(WorkStatusCategory? category) =>
        category is WorkStatusCategory.Done or WorkStatusCategory.Removed;
}
