using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Work;

namespace Wayd.Work.Application.WorkItems.Dtos;

/// <summary>
/// Work item metrics for a single sprint, as item counts and as estimates in the sprint's sizing method.
/// </summary>
public sealed record SprintWorkItemMetricsDto
{
    public Guid SprintId { get; init; }

    /// <summary>
    /// The estimate the sprint is measured in: its team's sizing method on the sprint's planned start, or
    /// Count for a sprint with no team. Under Count every estimate equals its item count.
    /// </summary>
    public SizingMethod SizingMethod { get; init; }

    public int TotalWorkItems { get; init; }

    /// <summary>The sum of every item's estimate in <see cref="SizingMethod"/>; unestimated items add nothing.</summary>
    public double TotalEstimate { get; init; }

    public int CompletedWorkItems { get; init; }
    public double CompletedEstimate { get; init; }

    public int InProgressWorkItems { get; init; }
    public double InProgressEstimate { get; init; }

    public int NotStartedWorkItems { get; init; }
    public double NotStartedEstimate { get; init; }

    /// <summary>Items with no value in <see cref="SizingMethod"/>. An estimate of 0 is an estimate.</summary>
    public int UnestimatedWorkItems { get; init; }

    /// <summary>
    /// Cycle-time rollup for the sprint. Carries count and total so callers can
    /// aggregate across sprints without average-of-averages bias.
    /// </summary>
    public required CycleTimeSummary CycleTime { get; init; }

    /// <summary>
    /// Creates metrics from a list of work items for a specific sprint, measured in <paramref name="sizingMethod"/>.
    /// </summary>
    public static SprintWorkItemMetricsDto FromWorkItems(Guid sprintId, SizingMethod sizingMethod, IEnumerable<WorkItem> workItems)
    {
        var items = workItems.ToList();
        double Estimate(IEnumerable<WorkItem> bucket) => bucket.Sum(w => WorkItemEstimate.Of(sizingMethod, w) ?? 0);

        var completed = items.Where(w =>
            w.StatusCategory == WorkStatusCategory.Done ||
            w.StatusCategory == WorkStatusCategory.Removed).ToList();

        var inProgress = items.Where(w => w.StatusCategory == WorkStatusCategory.Active).ToList();
        var notStarted = items.Where(w => w.StatusCategory == WorkStatusCategory.Proposed).ToList();

        // Cycle time: only items moved to Done with a valid Activated→Done span
        var doneItems = items.Where(w =>
            w.StatusCategory == WorkStatusCategory.Done &&
            w.ActivatedTimestamp.HasValue &&
            w.DoneTimestamp.HasValue &&
            w.DoneTimestamp.Value > w.ActivatedTimestamp.Value).ToList();

        var totalCycleTimeDays = doneItems.Sum(w =>
            (w.DoneTimestamp!.Value - w.ActivatedTimestamp!.Value).ToTimeSpan().TotalDays);

        return new SprintWorkItemMetricsDto
        {
            SprintId = sprintId,
            SizingMethod = sizingMethod,
            TotalWorkItems = items.Count,
            TotalEstimate = Estimate(items),
            CompletedWorkItems = completed.Count,
            CompletedEstimate = Estimate(completed),
            InProgressWorkItems = inProgress.Count,
            InProgressEstimate = Estimate(inProgress),
            NotStartedWorkItems = notStarted.Count,
            NotStartedEstimate = Estimate(notStarted),
            UnestimatedWorkItems = items.Count(w => WorkItemEstimate.Of(sizingMethod, w) is null),
            CycleTime = new CycleTimeSummary
            {
                WorkItemsCount = doneItems.Count,
                TotalCycleTimeDays = totalCycleTimeDays,
            },
        };
    }
}
