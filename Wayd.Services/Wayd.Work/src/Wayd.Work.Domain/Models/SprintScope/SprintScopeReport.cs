using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Work;

namespace Wayd.Work.Domain.Models.SprintScope;

/// <summary>
/// What a sprint committed to and what became of it, worked out from work item history between the sprint's
/// effective start and end. Worked out when read and never stored, so a change to history — a late sync, a
/// deleted item, a corrected actual date — changes the report on the next read.
/// </summary>
/// <remarks>
/// An item is in the sprint while its iteration is the sprint and its type is in the requirement tier. Each
/// item that was in the sprint between the effective start and end gets one entry and one outcome:
/// <list type="bullet">
/// <item><b>Committed</b> if it was in at the effective start, else <b>Added</b> if it entered before the
/// effective end. An item committed, taken out and put back is still committed.</item>
/// <item>Its outcome is read from its state when it was last in the sprint: at the effective end if it was
/// still in, or else just before it last left. Done is <b>Completed</b>, Removed is <b>Completed as
/// Removed</b>. Unfinished work still in, or moved to the team's next sprint on or after the sprint's last
/// day, is <b>Carried Over</b>; unfinished work that left any other way is <b>Descoped</b>.</item>
/// </list>
/// Work already Done or Removed before <see cref="SprintScopeWindow.FinishedWorkCutoff"/> is not in scope while
/// it stays finished: no work on it happened in the sprint. Reopened in the sprint, it is added from then on.
/// A report on a sprint that has not ended is worked out as of now: unfinished work still in it is
/// <b>Remaining</b>, since it may yet be completed. One that has not reached its commitment point has no scope.
/// A deleted item's history is deleted with it, so it is in no category.
/// </remarks>
public sealed class SprintScopeReport
{
    private SprintScopeReport(SprintScopeWindow window, SizingMethod sizingMethod, List<SprintScopeItem> items)
    {
        Window = window;
        SizingMethod = sizingMethod;
        Items = items;
        Totals = SprintScopeTotals.Of(items);
    }

    /// <summary>The instants the scope is measured between.</summary>
    public SprintScopeWindow Window { get; }

    /// <summary>The estimate the report is measured in.</summary>
    public SizingMethod SizingMethod { get; }

    /// <summary>Each work item that was in the sprint's scope, in no particular order.</summary>
    public IReadOnlyList<SprintScopeItem> Items { get; }

    /// <summary>The items summed by category.</summary>
    public SprintScopeTotals Totals { get; }

    /// <param name="periods">
    /// The history of every item that may have been in the sprint, in any order. Periods outside the window
    /// are ignored, so an item's whole history may be passed.
    /// </param>
    /// <param name="now">When the report is read; a sprint that has not ended is measured up to here.</param>
    public static SprintScopeReport Build(SprintScopeWindow window, SizingMethod sizingMethod, IEnumerable<SprintScopePeriod> periods, Instant now)
    {
        var items = Assess(window, sizingMethod, periods, now)
            .Select(a => a.Item)
            .ToList();

        return new SprintScopeReport(window, sizingMethod, items);
    }

    /// <summary>
    /// Each item that was in the sprint's scope as of <paramref name="now"/>, with the in-sprint periods it was
    /// judged on. The scope report and the sprint's burn charts both read it, so they count the same work.
    /// </summary>
    internal static List<AssessedItem> Assess(SprintScopeWindow window, SizingMethod sizingMethod, IEnumerable<SprintScopePeriod> periods, Instant now)
    {
        if (now <= window.Start)
            return [];

        var asOf = now < window.End ? now : window.End;
        return periods
            .GroupBy(p => p.WorkItemId)
            .Select(g => Classify(window, asOf, sizingMethod, g))
            .OfType<AssessedItem>()
            .ToList();
    }

    private static AssessedItem? Classify(SprintScopeWindow window, Instant asOf, SizingMethod sizingMethod, IEnumerable<SprintScopePeriod> itemPeriods)
    {
        // A zero-length period, left by clock skew between revisions, holds at no instant.
        var periods = itemPeriods
            .Where(p => p.ValidTo is null || p.ValidTo > p.ValidFrom)
            .OrderBy(p => p.ValidFrom)
            .ToList();

        bool InSprint(SprintScopePeriod p) => p.IterationId == window.SprintId && p.IsRequirement;
        double? EstimateOf(SprintScopePeriod p) => WorkItemEstimate.Of(sizingMethod, p.StoryPoints, p.Effort, p.Size);
        SprintScopePeriod? At(Instant instant) => periods.FirstOrDefault(p => p.Covers(instant));

        var finishedSince = FinishedSince(periods);
        var inSprint = periods
            .Where(p => InSprint(p) && p.ValidFrom < asOf && (p.ValidTo is null || p.ValidTo > window.Start))
            .Where(p => !(finishedSince.TryGetValue(p, out var finished) && finished < window.FinishedWorkCutoff))
            .ToList();
        if (inSprint.Count == 0)
            return null;

        var entryPeriod = inSprint[0];
        var committed = entryPeriod.Covers(window.Start);
        var entry = committed ? SprintScopeEntry.Committed : SprintScopeEntry.Added;
        Instant? addedAt = committed ? null : entryPeriod.ValidFrom;

        var last = inSprint[^1];
        var stillIn = last.Covers(asOf);
        var removedAt = stillIn ? null : last.ValidTo;

        var outcome = last.StatusCategory switch
        {
            WorkStatusCategory.Done => SprintScopeOutcome.Completed,
            WorkStatusCategory.Removed => SprintScopeOutcome.Removed,
            _ when stillIn && asOf < window.End => SprintScopeOutcome.Remaining,
            _ when stillIn => SprintScopeOutcome.CarriedOver,
            _ when MovedToNextSprint(window, removedAt!.Value, At(removedAt.Value)) => SprintScopeOutcome.CarriedOver,
            _ => SprintScopeOutcome.Descoped,
        };

        var item = new SprintScopeItem(
            periods[0].WorkItemId,
            entry,
            addedAt,
            outcome,
            removedAt,
            EstimateOf(entryPeriod),
            EstimateOf(last));

        return new AssessedItem(item, inSprint);
    }

    /// <summary>
    /// For each period in a Done or Removed status, when the item last became finished: the start of the
    /// unbroken run of finished periods holding it. Moving a finished item between sprints does not restart it.
    /// </summary>
    private static Dictionary<SprintScopePeriod, Instant> FinishedSince(List<SprintScopePeriod> periods)
    {
        var finishedSince = new Dictionary<SprintScopePeriod, Instant>();
        SprintScopePeriod? previous = null;
        foreach (var period in periods)
        {
            if (period.StatusCategory is WorkStatusCategory.Done or WorkStatusCategory.Removed)
            {
                finishedSince[period] = previous is not null
                    && previous.ValidTo == period.ValidFrom
                    && finishedSince.TryGetValue(previous, out var since)
                        ? since
                        : period.ValidFrom;
            }

            previous = period;
        }

        return finishedSince;
    }

    /// <param name="InSprint">
    /// The item's periods in the sprint that count toward its scope, in order: in the sprint's iteration, in
    /// the requirement tier, overlapping the window, and not finished before the sprint.
    /// </param>
    internal sealed record AssessedItem(SprintScopeItem Item, IReadOnlyList<SprintScopePeriod> InSprint);

    private static bool MovedToNextSprint(SprintScopeWindow window, Instant removedAt, SprintScopePeriod? after) =>
        window.NextSprintId is not null
            && removedAt >= window.LastDay
            && after?.IterationId == window.NextSprintId;
}
