using CSharpFunctionalExtensions;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Interfaces.Roadmaps;
using NodaTime;

namespace Wayd.Planning.Domain.Models.Roadmaps;

public sealed class RoadmapMilestone : BaseRoadmapItem
{
    private RoadmapMilestone() { }

    internal RoadmapMilestone(Guid roadmapId, string name, string? description, LocalDate date, Guid? parentId, string? color)
    {
        RoadmapId = roadmapId;
        Name = name;
        Description = description;
        Type = RoadmapItemType.Milestone;
        Date = date;
        ParentId = parentId;
        Color = color;
    }

    public LocalDate Date { get; private set; }

    /// <summary>
    /// Updates the Roadmap Milestone.
    /// </summary>
    /// <param name="roadmapMilestone"></param>
    /// <returns></returns>
    internal Result Update(IUpsertRoadmapMilestone roadmapMilestone, RoadmapActivity? parentActivity)
    {
        // TODO: this initial implementation requires going through the Roadmap to update the Roadmap Milestone. This is needed to verify permissions against the Roadmap within the Domain layer.

        if (ParentId != roadmapMilestone.ParentId)
        {
            var changeParentResult = ChangeParent(parentActivity);
            if (changeParentResult.IsFailure)
            {
                return changeParentResult;
            }
        }

        Name = roadmapMilestone.Name;
        Description = roadmapMilestone.Description;
        Date = roadmapMilestone.Date;
        Color = roadmapMilestone.Color;

        Parent?.RecalculateDateRangeFromChildren();

        return Result.Success();
    }

    /// <summary>
    /// Updates the date of the Roadmap Milestone.
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    internal Result UpdateDate(IUpsertRoadmapMilestoneDate date)
    {
        Date = date.Date;
        Parent?.RecalculateDateRangeFromChildren();
        return Result.Success();
    }

    /// <summary>
    /// Shifts the Milestone's date by the given number of days (negative shifts earlier). Used when
    /// an ancestor Activity is moved as a whole and the entire subtree shifts with it.
    /// </summary>
    /// <param name="days"></param>
    internal void ShiftDate(int days)
    {
        if (days == 0)
        {
            return;
        }

        Date = Date.PlusDays(days);
    }

    /// <summary>
    /// Creates a new Roadmap RoadmapMilestone.
    /// </summary>
    /// <param name="roadmapId"></param>
    /// <param name="parentId"></param>
    /// <param name="milestone"></param>
    /// <returns></returns>
    internal static RoadmapMilestone Create(Guid roadmapId, Guid? parentId, IUpsertRoadmapMilestone milestone)
    {
        return new RoadmapMilestone(roadmapId, milestone.Name, milestone.Description, milestone.Date, parentId, milestone.Color);
    }
}
