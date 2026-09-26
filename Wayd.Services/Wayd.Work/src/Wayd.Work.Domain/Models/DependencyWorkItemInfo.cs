using System.Linq.Expressions;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums.Work;
using NodaTime;

namespace Wayd.Work.Domain.Models;

public sealed record DependencyWorkItemInfo
{
    public Guid WorkItemId { get; set; }
    public WorkStatusCategory StatusCategory { get; set; }
    /// <summary>The last planned day of the work item's sprint, when it is in one.</summary>
    public LocalDate? PlannedOn { get; set; }

    /// <summary>
    /// Expression for projecting a WorkItem to DependencyWorkItemInfo in EF Core queries.
    /// This can be used directly in Select() statements for efficient database queries.
    /// </summary>
    public static Expression<Func<WorkItem, DependencyWorkItemInfo>> Projection => wi => new DependencyWorkItemInfo
    {
        WorkItemId = wi.Id,
        StatusCategory = wi.StatusCategory,
        PlannedOn = wi.Iteration != null && wi.Iteration.Type == IterationType.Sprint
            ? wi.Iteration.DateRange.End
            : null
    };

    public static DependencyWorkItemInfo Create(WorkItem workItem, Instant? now = null)
    {
        LocalDate? plannedOn = null;
        if (workItem.Iteration != null
            && workItem.Iteration.Type == IterationType.Sprint
            && workItem.Iteration.State != IterationState.Completed
            && (!now.HasValue || workItem.Iteration.DateRange.End >= now.Value.InUtc().Date))
        {
            plannedOn = workItem.Iteration.DateRange.End;
        }

        return new DependencyWorkItemInfo
        {
            WorkItemId = workItem.Id,
            StatusCategory = workItem.StatusCategory,
            PlannedOn = plannedOn
        };
    }
}
