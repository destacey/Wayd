using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Interfaces.Roadmaps;
using NodaTime;

namespace Wayd.Planning.Domain.Models.Roadmaps;

/// <summary>
/// An activity is a core component of the roadmap, representing a theme, project, piece of work or a group of related tasks.
/// </summary>
public sealed class RoadmapActivity : BaseRoadmapItem
{
    private LocalDateRange _dateRange = default!;
    private readonly List<BaseRoadmapItem> _children = [];

    private RoadmapActivity() { }

    internal RoadmapActivity(Guid roadmapId, string name, string? description, LocalDateRange dateRange, Guid? parentId, string? color, int order)
    {
        RoadmapId = roadmapId;
        Name = name;
        Description = description;
        Type = RoadmapItemType.Activity;
        DateRange = dateRange;
        ParentId = parentId;
        Color = color;
        Order = order;
    }

    /// <summary>
    /// The date range of the Roadmap Activity.
    /// </summary>
    public LocalDateRange DateRange
    {
        get => _dateRange;
        private set => _dateRange = Guard.Against.Null(value, nameof(DateRange));
    }

    /// <summary>
    /// The order of the Activity within the parent Roadmap or Roadmap Activity.
    /// </summary>
    public int Order { get; private set; }

    /// <summary>
    /// The children of the Roadmap Activity.
    /// </summary>
    public IReadOnlyList<BaseRoadmapItem> Children => _children.AsReadOnly();

    /// <summary>
    /// Updates the Roadmap Activity.
    /// </summary>
    /// <param name="roadmapActivity"></param>
    /// <returns></returns>
    internal Result Update(IUpsertRoadmapActivity roadmapActivity, RoadmapActivity? parentActivity)
    {
        // TODO: this initial implementation requires going through the Roadmap to update the Roadmap Activity. This is needed to verify permissions against the Roadmap within the Domain layer.

        var parentChanged = ParentId != roadmapActivity.ParentId;
        if (parentChanged)
        {
            var changeParentResult = ChangeParent(parentActivity);
            if (changeParentResult.IsFailure)
            {
                return changeParentResult;
            }
        }

        Name = roadmapActivity.Name;
        Description = roadmapActivity.Description;
        Color = roadmapActivity.Color;

        // Apply the dates. A pure shift (same duration) moves the whole subtree; otherwise the range
        // is resized in place and must still contain the children. When the parent is also changing
        // in this same update, treat the date change as a resize (a subtree shift would be ambiguous).
        return ApplyDateRange(roadmapActivity.DateRange, allowShift: !parentChanged);
    }

    /// <summary>
    /// Updates the date range of the Roadmap Activity. An Activity's range must contain all of its
    /// children, so a range that would fall inside the children (i.e. shrinking the parent behind a
    /// child) is rejected rather than silently clamped.
    /// </summary>
    /// <param name="dateRange"></param>
    /// <returns></returns>
    internal Result UpdateDateRange(IUpsertRoadmapActivityDateRange dateRange)
    {
        return ApplyDateRange(dateRange.DateRange, allowShift: true);
    }

    /// <summary>
    /// Applies a new date range to the Activity. When <paramref name="allowShift"/> is set and the
    /// new range preserves the duration and both endpoints move by the same amount (a pure shift),
    /// the entire subtree is moved by that delta so children keep their relative position. Otherwise
    /// the range is resized in place and must still contain all children (a range that would fall
    /// inside the children is rejected).
    /// </summary>
    private Result ApplyDateRange(LocalDateRange newRange, bool allowShift)
    {
        if (allowShift && _children.Count > 0 && TryGetShiftDays(newRange, out var days))
        {
            ShiftDates(days);
            Parent?.RecalculateDateRangeFromChildren();
            return Result.Success();
        }

        var containsChildrenResult = EnsureRangeContainsChildren(newRange);
        if (containsChildrenResult.IsFailure)
        {
            return containsChildrenResult;
        }

        DateRange = newRange;

        // The range already contains the children (guarded above); bubble up so any ancestor grows.
        Parent?.RecalculateDateRangeFromChildren();

        return Result.Success();
    }

    /// <summary>
    /// Determines whether the proposed range is a pure shift of the current range: same duration and
    /// both endpoints moved by the same non-zero number of days. Returns that day delta via
    /// <paramref name="days"/>.
    /// </summary>
    private bool TryGetShiftDays(LocalDateRange newRange, out int days)
    {
        var startDelta = Period.DaysBetween(DateRange.Start, newRange.Start);
        var endDelta = Period.DaysBetween(DateRange.End, newRange.End);

        days = startDelta;
        return startDelta != 0 && startDelta == endDelta;
    }

    /// <summary>
    /// Validates that the proposed range fully contains every child (child Activity/Timebox ranges
    /// and child Milestone dates). Returns a failure naming the offending child when the range would
    /// fall inside the children. Activities with no children always pass.
    /// </summary>
    private Result EnsureRangeContainsChildren(LocalDateRange proposedRange)
    {
        foreach (var child in _children)
        {
            var (name, start, end) = child switch
            {
                RoadmapActivity activity => (activity.Name, activity.DateRange.Start, activity.DateRange.End),
                RoadmapTimebox timebox => (timebox.Name, timebox.DateRange.Start, timebox.DateRange.End),
                RoadmapMilestone milestone => (milestone.Name, milestone.Date, milestone.Date),
                _ => (child.Name, proposedRange.Start, proposedRange.End)
            };

            if (start < proposedRange.Start || end > proposedRange.End)
            {
                return Result.Failure(
                    $"The date range must contain all child items. \"{name}\" falls outside the selected range.");
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Shifts this Activity and its entire subtree (all descendant Activities, Timeboxes, and
    /// Milestones) by the given number of days (negative shifts earlier). Relative positions are
    /// preserved, so containment is maintained automatically. This is the "move the whole branch"
    /// behavior used when a parent's date range is moved without changing its duration.
    /// </summary>
    /// <param name="days"></param>
    internal void ShiftDates(int days)
    {
        if (days == 0)
        {
            return;
        }

        DateRange = new LocalDateRange(DateRange.Start.PlusDays(days), DateRange.End.PlusDays(days));

        foreach (var child in _children)
        {
            switch (child)
            {
                case RoadmapActivity activity:
                    activity.ShiftDates(days);
                    break;
                case RoadmapTimebox timebox:
                    timebox.ShiftDates(days);
                    break;
                case RoadmapMilestone milestone:
                    milestone.ShiftDate(days);
                    break;
            }
        }
    }

    /// <summary>
    /// Recalculates this Activity's date range so that it always contains all of its children
    /// (child Activity/Timebox ranges and child Milestone dates). The range only grows: a parent
    /// keeps its own (wider) range and stretches to cover any child that falls outside it; it is
    /// never shrunk below its own stored range. When the range changes, the new range bubbles up
    /// to ancestor Activities so the whole branch stays consistent up to the root.
    /// </summary>
    internal void RecalculateDateRangeFromChildren()
    {
        if (_children.Count == 0)
        {
            return;
        }

        LocalDate? childrenStart = null;
        LocalDate? childrenEnd = null;

        foreach (var child in _children)
        {
            var (start, end) = child switch
            {
                RoadmapActivity activity => (activity.DateRange.Start, activity.DateRange.End),
                RoadmapTimebox timebox => (timebox.DateRange.Start, timebox.DateRange.End),
                RoadmapMilestone milestone => (milestone.Date, milestone.Date),
                _ => (DateRange.Start, DateRange.End)
            };

            childrenStart = childrenStart is null ? start : LocalDate.Min(childrenStart.Value, start);
            childrenEnd = childrenEnd is null ? end : LocalDate.Max(childrenEnd.Value, end);
        }

        var newStart = childrenStart is null ? DateRange.Start : LocalDate.Min(DateRange.Start, childrenStart.Value);
        var newEnd = childrenEnd is null ? DateRange.End : LocalDate.Max(DateRange.End, childrenEnd.Value);

        if (newStart == DateRange.Start && newEnd == DateRange.End)
        {
            return;
        }

        DateRange = new LocalDateRange(newStart, newEnd);

        Parent?.RecalculateDateRangeFromChildren();
    }

    /// <summary>
    /// Sets the order of the Roadmap Activity.
    /// </summary>
    /// <param name="order"></param>
    internal void SetOrder(int order)
    {
        Order = order;
    }

    /// <summary>
    /// Sets the order of a child Roadmap Activity and resets the order of the other child Roadmap Activities to match.
    /// </summary>
    /// <param name="activity"></param>
    /// <param name="order"></param>
    /// <returns></returns>
    internal Result SetChildActivityOrder(RoadmapActivity activity, int order)
    {
        // TODO: merge this with the SetActivityOrder on Roadmap.

        if (!_children.OfType<RoadmapActivity>().Any(x => x.Id == activity.Id))
        {
            return Result.Failure("Child activity not found.");
        }
        else if (activity.Order == order)
        {
            return Result.Success();
        }

        if (activity.Order < order)
        {
            foreach (var child in _children.OfType<RoadmapActivity>()
                .Where(x => x.Order > activity.Order && x.Order <= order))
            {
                child.SetOrder(child.Order - 1);
            }
        }
        else
        {
            foreach (var child in _children.OfType<RoadmapActivity>()
                .Where(x => x.Order >= order && x.Order < activity.Order))
            {
                child.SetOrder(child.Order + 1);
            }
        }

        activity.SetOrder(order);

        ResetChildActivitiesOrder();

        return Result.Success();
    }

    /// <summary>
    /// Resets the order of the child Roadmap Activities. This is used to remove any gaps in the order.
    /// </summary>
    internal void ResetChildActivitiesOrder()
    {
        int i = 1;
        foreach (var activity in _children.OfType<RoadmapActivity>().OrderBy(x => x.Order).ToArray())
        {
            activity.SetOrder(i);
            i++;
        }
    }

    /// <summary>
    /// Creates a new Roadmap Activity within an existing Roadmap Activity.
    /// </summary>
    /// <param name="roadmapId"></param>
    /// <param name="roadmapActivity"></param>
    /// <param name="order"></param>
    /// <returns></returns>
    internal RoadmapActivity CreateChildActivity(IUpsertRoadmapActivity roadmapActivity)
    {
        var order = Children.Count + 1;

        var newRoadmapActivity = new RoadmapActivity(RoadmapId, roadmapActivity.Name, roadmapActivity.Description, roadmapActivity.DateRange, Id, roadmapActivity.Color, order);

        _children.Add(newRoadmapActivity);
        newRoadmapActivity.LinkParent(this);

        RecalculateDateRangeFromChildren();

        return newRoadmapActivity;
    }

    /// <summary>
    /// Creates a new Roadmap Timebox within an existing Roadmap Activity.
    /// </summary>
    /// <param name="timebox"></param>
    /// <returns></returns>
    internal RoadmapTimebox CreateChildTimebox(IUpsertRoadmapTimebox timebox)
    {
        var newTimebox = RoadmapTimebox.Create(RoadmapId, Id, timebox);

        _children.Add(newTimebox);
        newTimebox.LinkParent(this);

        RecalculateDateRangeFromChildren();

        return newTimebox;
    }

    /// <summary>
    /// Creates a new Roadmap Milestone within an existing Roadmap Activity.
    /// </summary>
    /// <param name="milestone"></param>
    /// <returns></returns>
    internal RoadmapMilestone CreateChildMilestone(IUpsertRoadmapMilestone milestone)
    {
        var newMilestone = RoadmapMilestone.Create(RoadmapId, Id, milestone);

        _children.Add(newMilestone);
        newMilestone.LinkParent(this);

        RecalculateDateRangeFromChildren();

        return newMilestone;
    }

    /// <summary>
    /// Adds an existing child Roadmap Item to the Roadmap Activity.
    /// </summary>
    /// <param name="child"></param>
    /// <returns></returns>
    internal Result AddChild(BaseRoadmapItem child)
    {
        if (_children.Any(x => x.Id == child.Id))
        {
            return Result.Failure("Child already exists under this Roadmap Activity.");
        }

        switch (child)
        {
            case RoadmapActivity roadmapActivity:
                _children.Add(roadmapActivity);
                roadmapActivity.SetOrder(_children.Count);
                break;
            case RoadmapTimebox roadmapTimebox:
                _children.Add(roadmapTimebox);
                break;
            case RoadmapMilestone roadmapMilestone:
                _children.Add(roadmapMilestone);
                break;
            default:
                return Result.Failure("Child type not supported.");
        }

        RecalculateDateRangeFromChildren();

        return Result.Success();
    }

    /// <summary>
    /// Removes a child Roadmap Item from the Roadmap Activity.
    /// </summary>
    /// <param name="roadmapId"></param>
    /// <returns></returns>
    internal Result RemoveChild(Guid roadmapId)
    {
        var child = _children.FirstOrDefault(x => x.Id == roadmapId);

        if (child == null)
        {
            return Result.Failure("Child not found.");
        }

        _children.Remove(child);

        ResetChildActivitiesOrder();

        return Result.Success();
    }

    /// <summary>
    /// Gets the Roadmap Activity and all of its descendants.
    /// </summary>
    /// <returns></returns>
    internal List<Guid> GetSelfAndDescendants()
    {
        var ids = new List<Guid> { Id };

        foreach (var child in _children)
        {
            if (child is RoadmapActivity roadmapActivity)
            {
                ids.AddRange(roadmapActivity.GetSelfAndDescendants());
            }
            else
            {
                ids.Add(child.Id);
            }
        }

        return ids;
    }

    /// <summary>
    /// Creates a new Roadmap Activity at the root of the Roadmap.
    /// </summary>
    /// <param name="roadmapId"></param>
    /// <param name="activity"></param>
    /// <param name="order"></param>
    /// <returns></returns>
    internal static RoadmapActivity CreateRoot(Guid roadmapId, IUpsertRoadmapActivity activity, int order)
    {
        // TODO: this initial implementation requires going through the Roadmap to create the Roadmap Activity. This is needed to verify permissions against the Roadmap within the Domain layer.

        return new RoadmapActivity(roadmapId, activity.Name, activity.Description, activity.DateRange, null, activity.Color, order);

    }
}
