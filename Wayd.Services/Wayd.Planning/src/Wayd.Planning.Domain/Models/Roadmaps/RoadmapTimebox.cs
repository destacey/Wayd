using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Interfaces.Roadmaps;

namespace Wayd.Planning.Domain.Models.Roadmaps;

public sealed class RoadmapTimebox : BaseRoadmapItem
{
    private LocalDateRange _dateRange = default!;

    private RoadmapTimebox() { }

    internal RoadmapTimebox(Guid roadmapId, string name, string? description, LocalDateRange dateRange, Guid? parentId, string? color)
    {
        RoadmapId = roadmapId;
        Name = name;
        Description = description;
        Type = RoadmapItemType.Timebox;
        DateRange = dateRange;
        ParentId = parentId;
        Color = color;
    }

    /// <summary>
    /// The date range of the Roadmap Timebox.
    /// </summary>
    public LocalDateRange DateRange
    {
        get => _dateRange;
        private set => _dateRange = Guard.Against.Null(value, nameof(DateRange));
    }

    /// <summary>
    /// Updates the Roadmap Timebox.
    /// </summary>
    /// <param name="roadmapTimebox"></param>
    /// <returns></returns>
    internal Result Update(IUpsertRoadmapTimebox roadmapTimebox, RoadmapActivity? parentActivity)
    {
        // TODO: this initial implementation requires going through the Roadmap to update the Roadmap Timebox. This is needed to verify permissions against the Roadmap within the Domain layer.

        if (ParentId != roadmapTimebox.ParentId)
        {
            var changeParentResult = ChangeParent(parentActivity);
            if (changeParentResult.IsFailure)
            {
                return changeParentResult;
            }
        }

        Name = roadmapTimebox.Name;
        Description = roadmapTimebox.Description;
        DateRange = roadmapTimebox.DateRange;
        Color = roadmapTimebox.Color;

        Parent?.RecalculateDateRangeFromChildren();

        return Result.Success();
    }

    /// <summary>
    /// Updates the date range of the Roadmap Timebox.
    /// </summary>
    /// <param name="dateRange"></param>
    /// <returns></returns>
    internal Result UpdateDateRange(IUpsertRoadmapTimeboxDateRange dateRange)
    {
        DateRange = dateRange.DateRange;
        Parent?.RecalculateDateRangeFromChildren();
        return Result.Success();
    }

    /// <summary>
    /// Shifts the Timebox's date range by the given number of days (negative shifts earlier). Used
    /// when an ancestor Activity is moved as a whole and the entire subtree shifts with it.
    /// </summary>
    /// <param name="days"></param>
    internal void ShiftDates(int days)
    {
        if (days == 0)
        {
            return;
        }

        DateRange = new LocalDateRange(DateRange.Start.PlusDays(days), DateRange.End.PlusDays(days));
    }

    /// <summary>
    /// Creates a new Roadmap Timebox.
    /// </summary>
    /// <param name="roadmapId"></param>
    /// <param name="parentId"></param>
    /// <param name="timebox"></param>
    /// <returns></returns>
    internal static RoadmapTimebox Create(Guid roadmapId, Guid? parentId, IUpsertRoadmapTimebox timebox)
    {
        return new RoadmapTimebox(roadmapId, timebox.Name, timebox.Description, timebox.DateRange, parentId, timebox.Color);
    }
}
