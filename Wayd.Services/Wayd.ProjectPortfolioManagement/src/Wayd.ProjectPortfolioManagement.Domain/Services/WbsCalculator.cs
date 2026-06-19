using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Domain.Services;

/// <summary>
/// Domain service for calculating Work Breakdown Structure (WBS) codes for project tasks.
/// When phases are present, the WBS is prefixed with the phase order (e.g., "3.1.2" means phase 3, task 1, subtask 2).
/// </summary>
public static class WbsCalculator
{
    /// <summary>
    /// Calculates the WBS code for a specific task based on its position in the hierarchy.
    /// </summary>
    /// <param name="task">The task to calculate the WBS code for.</param>
    /// <param name="allTasks">All tasks in the project.</param>
    /// <returns>The WBS code (e.g., "1.2.1").</returns>
    public static string CalculateWbs(ProjectTask task, IEnumerable<ProjectTask> allTasks)
    {
        return CalculateWbs(task, allTasks, null);
    }

    /// <summary>
    /// Calculates the WBS code for a specific task, including phase-level numbering when phases are present.
    /// </summary>
    /// <param name="task">The task to calculate the WBS code for.</param>
    /// <param name="allTasks">All tasks in the project.</param>
    /// <param name="phases">The project phases, or null if no lifecycle is assigned.</param>
    /// <returns>The WBS code (e.g., "3.1.2" where 3 is the phase order).</returns>
    public static string CalculateWbs(ProjectTask task, IEnumerable<ProjectTask> allTasks, IEnumerable<ProjectPhase>? phases)
    {
        var path = new List<int>();
        var current = task;
        var taskList = allTasks.ToList();
        var phaseList = phases?.ToList();

        // Build path from current task to root
        while (current is not null)
        {
            // For root tasks with phases, scope siblings to the same phase
            var siblings = current.ParentId.HasValue
                ? taskList.Where(t => t.ParentId == current.ParentId)
                : phaseList is not null
                    ? taskList.Where(t => t.ParentId is null && t.ProjectPhaseId == current.ProjectPhaseId)
                    : taskList.Where(t => t.ParentId is null);

            var orderedSiblings = siblings.OrderBy(t => t.Order).ToList();

            var index = orderedSiblings.FindIndex(t => t.Id == current.Id);
            if (index >= 0)
            {
                path.Insert(0, index + 1); // 1-based indexing
            }

            // Navigate to parent
            current = current.ParentId.HasValue
                ? taskList.FirstOrDefault(t => t.Id == current.ParentId)
                : null;
        }

        // Prefix with phase order if phases are provided
        if (phaseList is not null)
        {
            var phase = phaseList.FirstOrDefault(p => p.Id == task.ProjectPhaseId);
            if (phase is not null)
            {
                path.Insert(0, phase.Order);
            }
        }

        return string.Join(".", path);
    }

    /// <summary>
    /// Calculates WBS codes for all tasks in a collection.
    /// </summary>
    /// <param name="tasks">The tasks to calculate WBS codes for.</param>
    /// <returns>A dictionary mapping task IDs to their WBS codes.</returns>
    public static Dictionary<Guid, string> CalculateAllWbs(IEnumerable<ProjectTask> tasks)
    {
        return CalculateAllWbs(tasks, null);
    }

    /// <summary>
    /// Calculates WBS codes for all tasks in a collection, including phase-level numbering.
    /// </summary>
    /// <param name="tasks">The tasks to calculate WBS codes for.</param>
    /// <param name="phases">The project phases, or null if no lifecycle is assigned.</param>
    /// <returns>A dictionary mapping task IDs to their WBS codes.</returns>
    public static Dictionary<Guid, string> CalculateAllWbs(IEnumerable<ProjectTask> tasks, IEnumerable<ProjectPhase>? phases)
    {
        var result = new Dictionary<Guid, string>();
        var taskList = tasks.ToList();
        var phaseList = phases?.ToList();

        foreach (var task in taskList)
        {
            result[task.Id] = CalculateWbs(task, taskList, phaseList);
        }

        return result;
    }
}
