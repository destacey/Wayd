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
    private readonly List<StoryMapTask> _tasks = [];

    private Step() { }

    internal Step(Guid goalId, string name, int order)
    {
        GoalId = goalId;
        Name = name;
        Order = order;
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
    public int Order { get; private set; }

    /// <summary>
    /// The personas tagged on this step.
    /// </summary>
    public IReadOnlyList<Guid> PersonaIds => _personaIds.AsReadOnly();

    /// <summary>
    /// The tasks beneath this step, across all swim lanes.
    /// </summary>
    public IReadOnlyList<StoryMapTask> Tasks => _tasks.AsReadOnly();

    internal void Rename(string name) => Name = name;

    internal void SetOrder(int order) => Order = order;

    internal void ChangeGoal(Guid goalId, int order)
    {
        GoalId = goalId;
        Order = order;
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

    internal StoryMapTask AddTask(Guid swimLaneId, string title)
    {
        int nextOrder = NextTaskOrder(swimLaneId);
        var task = new StoryMapTask(Id, swimLaneId, title, nextOrder);
        _tasks.Add(task);
        return task;
    }

    internal void AttachTask(StoryMapTask task) => _tasks.Add(task);

    internal Result<StoryMapTask> RemoveTask(Guid taskId)
    {
        var task = _tasks.FirstOrDefault(x => x.Id == taskId);
        if (task is null)
            return Result.Failure<StoryMapTask>("Task does not exist on this step.");

        _tasks.Remove(task);
        return task;
    }

    internal Result<StoryMapTask> GetTask(Guid taskId)
    {
        var task = _tasks.FirstOrDefault(x => x.Id == taskId);
        return task is not null
            ? task
            : Result.Failure<StoryMapTask>("Task does not exist on this step.");
    }

    /// <summary>
    /// The next sort order for a task landing in the given swim lane within this step.
    /// </summary>
    internal int NextTaskOrder(Guid swimLaneId)
    {
        var laneTasks = _tasks.Where(x => x.SwimLaneId == swimLaneId).ToList();
        return laneTasks.Count > 0 ? laneTasks.Max(x => x.Order) + 1 : 0;
    }

    /// <summary>
    /// Reassigns every task in the given swim lane to the target swim lane, appending them after any existing
    /// tasks there. Used when a swim lane is removed and its tasks return to the default swim lane.
    /// </summary>
    internal int ReassignTasksToSwimLane(Guid fromSwimLaneId, Guid toSwimLaneId)
    {
        var tasksToMove = _tasks.Where(x => x.SwimLaneId == fromSwimLaneId).OrderBy(x => x.Order).ToList();
        foreach (var task in tasksToMove)
        {
            task.ReassignSwimLane(toSwimLaneId, NextTaskOrder(toSwimLaneId));
        }
        return tasksToMove.Count;
    }

    #endregion Tasks
}
