using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Domain.Services;

/// <summary>
/// Domain service for calculating Work Breakdown Structure (WBS) codes for project tasks.
/// When stages are present, the WBS is prefixed with the stage order (e.g., "3.1.2" means stage 3, task 1, subtask 2).
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
    /// Calculates the WBS code for a specific task, including stage-level numbering when stages are present.
    /// </summary>
    /// <param name="task">The task to calculate the WBS code for.</param>
    /// <param name="allTasks">All tasks in the project.</param>
    /// <param name="stages">The project stages, or null if no lifecycle is assigned.</param>
    /// <returns>The WBS code (e.g., "3.1.2" where 3 is the stage order).</returns>
    public static string CalculateWbs(ProjectTask task, IEnumerable<ProjectTask> allTasks, IEnumerable<ProjectStage>? stages)
    {
        var path = new List<int>();
        var current = task;
        var taskList = allTasks.ToList();
        var stageList = stages?.ToList();

        // Build path from current task to root
        while (current is not null)
        {
            // For root tasks with stages, scope siblings to the same stage
            var siblings = current.ParentId.HasValue
                ? taskList.Where(t => t.ParentId == current.ParentId)
                : stageList is not null
                    ? taskList.Where(t => t.ParentId is null && t.ProjectStageId == current.ProjectStageId)
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

        // Prefix with stage order if stages are provided
        if (stageList is not null)
        {
            var stage = stageList.FirstOrDefault(p => p.Id == task.ProjectStageId);
            if (stage is not null)
            {
                path.Insert(0, stage.Order);
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
    /// Calculates WBS codes for all tasks in a collection, including stage-level numbering.
    /// </summary>
    /// <param name="tasks">The tasks to calculate WBS codes for.</param>
    /// <param name="stages">The project stages, or null if no lifecycle is assigned.</param>
    /// <returns>A dictionary mapping task IDs to their WBS codes.</returns>
    public static Dictionary<Guid, string> CalculateAllWbs(IEnumerable<ProjectTask> tasks, IEnumerable<ProjectStage>? stages)
    {
        var result = new Dictionary<Guid, string>();
        var taskList = tasks.ToList();
        var stageList = stages?.ToList();

        foreach (var task in taskList)
        {
            result[task.Id] = CalculateWbs(task, taskList, stageList);
        }

        return result;
    }
}
