using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

/// <summary>
/// What <see cref="DeliverableProgressCalculator"/> reads of one plan task.
/// </summary>
/// <param name="Id">The task.</param>
/// <param name="ParentId">Its parent task, or null for a top-level task.</param>
/// <param name="Type">Whether it is a task or a milestone.</param>
/// <param name="Status">Its status; a canceled task is left out.</param>
/// <param name="Progress">Its own progress, 0 to 100.</param>
/// <param name="PlannedDateRange">Its planned dates, which weight it.</param>
public sealed record DeliverableProgressTask(
    Guid Id,
    Guid? ParentId,
    ProjectTaskType Type,
    Enums.TaskStatus Status,
    decimal Progress,
    FlexibleDateRange? PlannedDateRange);

/// <summary>
/// Derives a deliverable's progress from the plan tasks linked to it, when read. Progress is never stored, so no
/// task or deliverable edit has to recalculate it.
/// </summary>
/// <remarks>
/// The rule is the plan's summary rollup: the mean progress of the leaf tasks beneath the linked tasks, weighted
/// by planned duration in days. A linked task whose ancestor is also linked adds nothing, so shared work is
/// counted once. Milestones, undated tasks and open-ended tasks weigh one day, so a deliverable served only by
/// them still shows progress. The plan Gantt weights those by zero, so its summary bars can differ.
/// <para>
/// A canceled task, with everything beneath it, is left out: work that will not be done cannot hold a deliverable
/// short of 100%. A completed task counts at its recorded progress.
/// </para>
/// <para>
/// Built once over a project's whole plan, then asked for each deliverable: a leaf beneath a linked task is
/// found only if every task of the plan was supplied.
/// </para>
/// </remarks>
public sealed class DeliverableProgressCalculator
{
    private readonly Dictionary<Guid, DeliverableProgressTask> _tasks;
    private readonly ILookup<Guid, DeliverableProgressTask> _children;

    /// <summary>Indexes a project's plan.</summary>
    /// <param name="planTasks">Every task of the project's plan.</param>
    public DeliverableProgressCalculator(IEnumerable<DeliverableProgressTask> planTasks)
    {
        _tasks = planTasks.ToDictionary(t => t.Id);
        _children = _tasks.Values
            .Where(t => t.ParentId is not null)
            .ToLookup(t => t.ParentId!.Value);
    }

    /// <summary>
    /// The progress of a deliverable served by <paramref name="linkedTaskIds"/>, 0 to 100 to two decimal places,
    /// or null when none of them is in the plan or all of them are canceled: a deliverable with no work to count
    /// has no progress, not 0%.
    /// </summary>
    public decimal? Calculate(IEnumerable<Guid> linkedTaskIds)
    {
        var linked = linkedTaskIds.Where(_tasks.ContainsKey).ToHashSet();
        if (linked.Count == 0)
            return null;

        decimal weighted = 0;
        decimal weight = 0;

        // Guards against a parent cycle in bad data, which would otherwise never end the descent.
        var visited = new HashSet<Guid>();

        foreach (var rootId in linked.Where(id => !HasLinkedAncestor(id, linked)))
        {
            var pending = new Stack<DeliverableProgressTask>();
            pending.Push(_tasks[rootId]);

            while (pending.TryPop(out var task))
            {
                if (task.Status == Enums.TaskStatus.Canceled || !visited.Add(task.Id))
                    continue;

                var children = _children[task.Id];
                if (children.Any())
                {
                    foreach (var child in children)
                        pending.Push(child);
                    continue;
                }

                var taskWeight = Weight(task);
                weighted += task.Progress * taskWeight;
                weight += taskWeight;
            }
        }

        if (weight == 0)
            return null;

        return Math.Round(weighted / weight, 2, MidpointRounding.AwayFromZero);
    }

    private bool HasLinkedAncestor(Guid taskId, HashSet<Guid> linked)
    {
        var visited = new HashSet<Guid> { taskId };
        var parentId = _tasks[taskId].ParentId;

        while (parentId is { } id && _tasks.TryGetValue(id, out var parent) && visited.Add(id))
        {
            if (linked.Contains(id))
                return true;
            parentId = parent.ParentId;
        }

        return false;
    }

    private static decimal Weight(DeliverableProgressTask task) =>
        task.Type != ProjectTaskType.Milestone && task.PlannedDateRange is { End: not null } range
            ? range.Days
            : 1;
}
