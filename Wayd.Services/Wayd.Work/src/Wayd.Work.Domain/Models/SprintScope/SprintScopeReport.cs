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

    public SprintScopeWindow Window { get; }

    /// <summary>The estimate the report is measured in.</summary>
    public SizingMethod SizingMethod { get; }

    public IReadOnlyList<SprintScopeItem> Items { get; }

    public SprintScopeTotals Totals { get; }

    /// <param name="periods">
    /// The history of every item that may have been in the sprint, in any order. Periods outside the window
    /// are ignored, so an item's whole history may be passed.
    /// </param>
    public static SprintScopeReport Build(SprintScopeWindow window, SizingMethod sizingMethod, IEnumerable<SprintScopePeriod> periods)
    {
        var items = periods
            .GroupBy(p => p.WorkItemId)
            .Select(g => Classify(window, sizingMethod, g))
            .OfType<SprintScopeItem>()
            .ToList();

        return new SprintScopeReport(window, sizingMethod, items);
    }

    private static SprintScopeItem? Classify(SprintScopeWindow window, SizingMethod sizingMethod, IEnumerable<SprintScopePeriod> itemPeriods)
    {
        // A zero-length period, left by clock skew between revisions, holds at no instant.
        var periods = itemPeriods
            .Where(p => p.ValidTo is null || p.ValidTo > p.ValidFrom)
            .OrderBy(p => p.ValidFrom)
            .ToList();

        bool InSprint(SprintScopePeriod p) => p.IterationId == window.SprintId && p.IsRequirement;
        double? EstimateOf(SprintScopePeriod p) => WorkItemEstimate.Of(sizingMethod, p.StoryPoints, p.Effort, p.Size);
        SprintScopePeriod? At(Instant instant) => periods.FirstOrDefault(p => p.Covers(instant));

        var atStart = At(window.Start);
        var inSprint = periods
            .Where(p => InSprint(p) && p.ValidFrom < window.End && (p.ValidTo is null || p.ValidTo > window.Start))
            .ToList();
        if (inSprint.Count == 0)
            return null;

        SprintScopeEntry entry;
        Instant? enteredAt;
        SprintScopePeriod entryPeriod;
        if (atStart is not null && InSprint(atStart))
        {
            entry = SprintScopeEntry.Committed;
            enteredAt = null;
            entryPeriod = atStart;
        }
        else
        {
            entry = SprintScopeEntry.Added;
            entryPeriod = inSprint[0];
            enteredAt = entryPeriod.ValidFrom;
        }

        var last = inSprint[^1];
        var stillIn = last.Covers(window.End);
        var leftAt = stillIn ? null : last.ValidTo;

        var outcome = last.StatusCategory switch
        {
            WorkStatusCategory.Done => SprintScopeOutcome.Completed,
            WorkStatusCategory.Removed => SprintScopeOutcome.Removed,
            _ when stillIn => SprintScopeOutcome.CarriedOver,
            _ when MovedToNextSprint(window, leftAt!.Value, At(leftAt.Value)) => SprintScopeOutcome.CarriedOver,
            _ => SprintScopeOutcome.Descoped,
        };

        return new SprintScopeItem(
            periods[0].WorkItemId,
            entry,
            enteredAt,
            outcome,
            leftAt,
            EstimateOf(entryPeriod),
            EstimateOf(last));
    }

    private static bool MovedToNextSprint(SprintScopeWindow window, Instant leftAt, SprintScopePeriod? after) =>
        window.NextSprintId is not null
            && leftAt >= window.LastDay
            && after?.IterationId == window.NextSprintId;
}
