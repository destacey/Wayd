using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Work.Application.WorkItems.Dtos;

/// <summary>
/// Progress data for a collection of work items.  Work items with a status category of Proposed, Active, and Done are counted.  Work items with a status category of Removed are not included
/// </summary>
public record WorkItemProgressRollupDto
{
    public int Proposed { get; protected set; }
    public int Active { get; protected set; }
    public int Done { get; protected set; }
    public int Total => Proposed + Active + Done;

    public static WorkItemProgressRollupDto Create(int proposed, int active, int done)
    {
        return new WorkItemProgressRollupDto
        {
            Proposed = proposed,
            Active = active,
            Done = done
        };
    }

    public static WorkItemProgressRollupDto CreateEmpty()
    {
        return Create(0, 0, 0);
    }

    public static WorkItemProgressRollupDto Create(List<WorkItemProgressStateDto> workItems)
    {
        var counts = workItems
            .GroupBy(w => w.StatusCategory)
            .ToDictionary(g => g.Key, g => g.Count());

        return new WorkItemProgressRollupDto
        {
            Proposed = counts.GetValueOrDefault(StatusCategory.Proposed, 0),
            Active = counts.GetValueOrDefault(StatusCategory.Active, 0),
            Done = counts.GetValueOrDefault(StatusCategory.Done, 0)
        };
    }

    public static WorkItemProgressRollupDto Create(List<WorkItemProgressRollupDto> rollups)
    {
        return new WorkItemProgressRollupDto
        {
            Proposed = rollups.Sum(r => r.Proposed),
            Active = rollups.Sum(r => r.Active),
            Done = rollups.Sum(r => r.Done)
        };
    }
}
