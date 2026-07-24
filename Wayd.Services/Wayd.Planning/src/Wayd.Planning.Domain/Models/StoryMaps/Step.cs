using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;

namespace Wayd.Planning.Domain.Models.StoryMaps;

/// <summary>
/// A step on a Story Map — how the user accomplishes a goal. Steps sit beneath their goal, read
/// left to right, and hold the tasks that make the step possible.
/// </summary>
public sealed class Step : BaseAuditableEntity
{
    private readonly List<Guid> _personaIds = [];
    private readonly List<StoryTask> _tasks = [];

    private Step() { }

    internal Step(Guid goalId, string name, int sortOrder)
    {
        GoalId = goalId;
        Name = name;
        SortOrder = sortOrder;
    }

    /// <summary>
    /// The goal this step sits beneath.
    /// </summary>
    public Guid GoalId { get; private set; }

    /// <summary>
    /// The name of the step.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The order of the step within its goal.
    /// </summary>
    public int SortOrder { get; private set; }

    /// <summary>
    /// The personas tagged on this step.
    /// </summary>
    public IReadOnlyList<Guid> PersonaIds => _personaIds.AsReadOnly();

    /// <summary>
    /// The tasks beneath this step, across all lanes.
    /// </summary>
    public IReadOnlyList<StoryTask> Tasks => _tasks.AsReadOnly();

    internal void Rename(string name) => Name = name;

    internal void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    internal void ChangeGoal(Guid goalId, int sortOrder)
    {
        GoalId = goalId;
        SortOrder = sortOrder;
    }

    #region Personas

    internal void SetPersonas(IEnumerable<Guid> personaIds)
    {
        _personaIds.Clear();
        _personaIds.AddRange(personaIds.Distinct());
    }

    internal void RemovePersona(Guid personaId) => _personaIds.Remove(personaId);

    #endregion Personas

    #region Tasks

    internal StoryTask AddTask(Guid laneId, string title)
    {
        int nextOrder = NextTaskOrder(laneId);
        var task = new StoryTask(Id, laneId, title, nextOrder);
        _tasks.Add(task);
        return task;
    }

    internal void AttachTask(StoryTask task) => _tasks.Add(task);

    internal Result<StoryTask> RemoveTask(Guid taskId)
    {
        var task = _tasks.FirstOrDefault(x => x.Id == taskId);
        if (task is null)
            return Result.Failure<StoryTask>("Task does not exist on this step.");

        _tasks.Remove(task);
        return task;
    }

    internal Result<StoryTask> GetTask(Guid taskId)
    {
        var task = _tasks.FirstOrDefault(x => x.Id == taskId);
        return task is not null
            ? task
            : Result.Failure<StoryTask>("Task does not exist on this step.");
    }

    /// <summary>
    /// The next sort order for a task landing in the given lane within this step.
    /// </summary>
    internal int NextTaskOrder(Guid laneId)
    {
        var laneTasks = _tasks.Where(x => x.LaneId == laneId).ToList();
        return laneTasks.Count > 0 ? laneTasks.Max(x => x.SortOrder) + 1 : 0;
    }

    /// <summary>
    /// Reassigns every task in the given lane to the target lane, appending them after any existing
    /// tasks there. Used when a lane is removed and its tasks return to the default lane.
    /// </summary>
    internal int ReassignTasksToLane(Guid fromLaneId, Guid toLaneId)
    {
        var tasksToMove = _tasks.Where(x => x.LaneId == fromLaneId).OrderBy(x => x.SortOrder).ToList();
        foreach (var task in tasksToMove)
        {
            task.ReassignLane(toLaneId, NextTaskOrder(toLaneId));
        }
        return tasksToMove.Count;
    }

    #endregion Tasks
}
