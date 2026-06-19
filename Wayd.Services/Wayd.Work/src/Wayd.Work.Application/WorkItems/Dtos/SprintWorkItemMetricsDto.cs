using Wayd.Common.Domain.Enums.Work;

namespace Wayd.Work.Application.WorkItems.Dtos;

/// <summary>
/// Work item metrics for a single sprint.
/// </summary>
public sealed record SprintWorkItemMetricsDto
{
    public Guid SprintId { get; init; }

    public int TotalWorkItems { get; init; }
    public double TotalStoryPoints { get; init; }

    public int CompletedWorkItems { get; init; }
    public double CompletedStoryPoints { get; init; }

    public int InProgressWorkItems { get; init; }
    public double InProgressStoryPoints { get; init; }

    public int NotStartedWorkItems { get; init; }
    public double NotStartedStoryPoints { get; init; }

    public int MissingStoryPointsCount { get; init; }

    /// <summary>
    /// Cycle-time rollup for the sprint. Carries count and total so callers can
    /// aggregate across sprints without average-of-averages bias.
    /// </summary>
    public required CycleTimeSummary CycleTime { get; init; }

    /// <summary>
    /// Creates metrics from a list of work items for a specific sprint.
    /// </summary>
    public static SprintWorkItemMetricsDto FromWorkItems(Guid sprintId, IEnumerable<WorkItem> workItems)
    {
        var items = workItems.ToList();

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
            TotalWorkItems = items.Count,
            TotalStoryPoints = items.Sum(w => w.StoryPoints ?? 0),
            CompletedWorkItems = completed.Count,
            CompletedStoryPoints = completed.Sum(w => w.StoryPoints ?? 0),
            InProgressWorkItems = inProgress.Count,
            InProgressStoryPoints = inProgress.Sum(w => w.StoryPoints ?? 0),
            NotStartedWorkItems = notStarted.Count,
            NotStartedStoryPoints = notStarted.Sum(w => w.StoryPoints ?? 0),
            MissingStoryPointsCount = items.Count(w => !w.StoryPoints.HasValue || w.StoryPoints == 0),
            CycleTime = new CycleTimeSummary
            {
                WorkItemsCount = doneItems.Count,
                TotalCycleTimeDays = totalCycleTimeDays,
            },
        };
    }
}
