using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Interfaces;
using NodaTime;

namespace Wayd.Planning.Domain.Models.StoryMaps;

/// <summary>
/// A Story Map — a persistent surface for describing what a product should do, organized the way
/// story mapping is practiced: a horizontal narrative of goals sliced vertically into what gets
/// built when. This is the aggregate root; goals, steps, tasks, lanes, and personas are all owned
/// by it and mutated only through it.
/// </summary>
public sealed class StoryMap : BaseSoftDeletableEntity, IHasIdAndKey
{
    private readonly List<Goal> _goals = [];
    private readonly List<SwimLane> _lanes = [];
    private readonly List<Persona> _personas = [];

    private StoryMap() { }

    private StoryMap(string name, string? description, string ownerId)
    {
        Name = name;
        Description = description;
        OwnerId = ownerId;
        Status = WorkStatusCategory.Active;
    }

    /// <summary>
    /// The unique key of the Story Map. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The name of the Story Map.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The description of the Story Map.
    /// </summary>
    public string? Description
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>
    /// The user who owns this Story Map. Defaults to the creator and can be reassigned.
    /// </summary>
    public string OwnerId
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(OwnerId));
    } = null!;

    public User? Owner { get; private set; }

    /// <summary>
    /// The lifecycle status of the map. Active while in use, Removed once archived.
    /// </summary>
    public WorkStatusCategory Status { get; private set; }

    /// <summary>
    /// The goals on the map, in order (the top-row narrative).
    /// </summary>
    public IReadOnlyList<Goal> Goals => [.. _goals.OrderBy(x => x.SortOrder)];

    /// <summary>
    /// The swim lanes on the map, in order. The default lane is always first.
    /// </summary>
    public IReadOnlyList<SwimLane> Lanes => [.. _lanes.OrderBy(x => x.SortOrder)];

    /// <summary>
    /// The personas defined on the map.
    /// </summary>
    public IReadOnlyList<Persona> Personas => _personas.AsReadOnly();

    private SwimLane DefaultLane => _lanes.Single(x => x.IsDefault);

    #region Map lifecycle

    /// <summary>
    /// Updates the map's name and description.
    /// </summary>
    public Result Update(string name, string? description)
    {
        try
        {
            Name = name;
            Description = description;
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Reassigns the owner of the map.
    /// </summary>
    public Result ChangeOwner(string ownerId)
    {
        try
        {
            OwnerId = ownerId;
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Archives the map. Only active maps can be archived.
    /// </summary>
    public Result Archive()
    {
        if (Status != WorkStatusCategory.Active)
            return Result.Failure("Only active story maps can be archived.");

        Status = WorkStatusCategory.Removed;
        return Result.Success();
    }

    #endregion Map lifecycle

    #region Goals

    /// <summary>
    /// Adds a goal to the map. The goal comes with one step already created, since an empty goal
    /// gives people nothing to react to.
    /// </summary>
    public Result<Goal> AddGoal(string name, string firstStepName)
    {
        try
        {
            int nextOrder = _goals.Count > 0 ? _goals.Max(x => x.SortOrder) + 1 : 0;
            var goal = new Goal(Id, name, nextOrder);
            goal.AddStep(firstStepName);
            _goals.Add(goal);
            return goal;
        }
        catch (Exception ex)
        {
            return Result.Failure<Goal>(ex.Message);
        }
    }

    public Result RenameGoal(Guid goalId, string name)
    {
        var goalResult = GetGoal(goalId);
        if (goalResult.IsFailure)
            return Result.Failure(goalResult.Error);

        try
        {
            goalResult.Value.Rename(name);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Reorders a goal to a new position among the map's goals.
    /// </summary>
    public Result ReorderGoal(Guid goalId, int newOrder)
    {
        var goalResult = GetGoal(goalId);
        if (goalResult.IsFailure)
            return Result.Failure(goalResult.Error);

        Reorder(_goals, goalResult.Value, newOrder, x => x.SortOrder, (x, o) => x.SetSortOrder(o));
        return Result.Success();
    }

    /// <summary>
    /// Deletes a goal, along with its steps and their tasks. A map must keep at least one goal.
    /// </summary>
    public Result DeleteGoal(Guid goalId)
    {
        var goalResult = GetGoal(goalId);
        if (goalResult.IsFailure)
            return Result.Failure(goalResult.Error);

        if (_goals.Count == 1)
            return Result.Failure("A story map must have at least one goal.");

        _goals.Remove(goalResult.Value);
        ResetGoalOrder();
        return Result.Success();
    }

    private Result<Goal> GetGoal(Guid goalId)
    {
        var goal = _goals.FirstOrDefault(x => x.Id == goalId);
        return goal is not null
            ? goal
            : Result.Failure<Goal>("Goal does not exist on this story map.");
    }

    private void ResetGoalOrder() => Renumber(_goals, x => x.SortOrder, (x, o) => x.SetSortOrder(o));

    #endregion Goals

    #region Steps

    /// <summary>
    /// Adds a step to a goal.
    /// </summary>
    public Result<Step> AddStep(Guid goalId, string name)
    {
        var goalResult = GetGoal(goalId);
        if (goalResult.IsFailure)
            return Result.Failure<Step>(goalResult.Error);

        try
        {
            return goalResult.Value.AddStep(name);
        }
        catch (Exception ex)
        {
            return Result.Failure<Step>(ex.Message);
        }
    }

    public Result RenameStep(Guid stepId, string name)
    {
        var located = LocateStep(stepId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        try
        {
            located.Value.Step.Rename(name);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Reorders a step within its goal.
    /// </summary>
    public Result ReorderStep(Guid stepId, int newOrder)
    {
        var located = LocateStep(stepId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        var (goal, step) = located.Value;
        var stepsField = MutableSteps(goal);
        Reorder(stepsField, step, newOrder, x => x.SortOrder, (x, o) => x.SetSortOrder(o));
        return Result.Success();
    }

    /// <summary>
    /// Moves a step into a different goal at the given order. A step cannot leave a goal that would
    /// then have no steps.
    /// </summary>
    public Result MoveStep(Guid stepId, Guid targetGoalId, int newOrder)
    {
        var located = LocateStep(stepId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        var (sourceGoal, step) = located.Value;
        if (sourceGoal.Id == targetGoalId)
            return ReorderStep(stepId, newOrder);

        var targetGoalResult = GetGoal(targetGoalId);
        if (targetGoalResult.IsFailure)
            return Result.Failure(targetGoalResult.Error);

        if (sourceGoal.StepCount == 1)
            return Result.Failure("A goal must have at least one step.");

        var targetGoal = targetGoalResult.Value;

        var detachResult = sourceGoal.DetachStep(step);
        if (detachResult.IsFailure)
            return detachResult;

        step.ChangeGoal(targetGoal.Id, targetGoal.NextStepOrder());
        targetGoal.AttachStep(step);
        targetGoal.ResetStepOrder();

        // Place the step at the requested order within the target goal.
        Reorder(MutableSteps(targetGoal), step, newOrder, x => x.SortOrder, (x, o) => x.SetSortOrder(o));
        return Result.Success();
    }

    /// <summary>
    /// Deletes a step and its tasks. A goal must keep at least one step.
    /// </summary>
    public Result DeleteStep(Guid stepId)
    {
        var located = LocateStep(stepId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        var (goal, _) = located.Value;
        var removeResult = goal.RemoveStep(stepId);
        return removeResult.IsFailure ? Result.Failure(removeResult.Error) : Result.Success();
    }

    private Result<(Goal Goal, Step Step)> LocateStep(Guid stepId)
    {
        foreach (var goal in _goals)
        {
            var stepResult = goal.GetStep(stepId);
            if (stepResult.IsSuccess)
                return (goal, stepResult.Value);
        }
        return Result.Failure<(Goal, Step)>("Step does not exist on this story map.");
    }

    #endregion Steps

    #region Tasks

    /// <summary>
    /// Adds a task to a step. Without a lane, the task lands in the default lane.
    /// </summary>
    public Result<StoryTask> AddTask(Guid stepId, string title, Guid? laneId = null)
    {
        var located = LocateStep(stepId);
        if (located.IsFailure)
            return Result.Failure<StoryTask>(located.Error);

        var lane = laneId is null ? DefaultLane : _lanes.FirstOrDefault(x => x.Id == laneId.Value);
        if (lane is null)
            return Result.Failure<StoryTask>("Lane does not exist on this story map.");

        try
        {
            return located.Value.Step.AddTask(lane.Id, title);
        }
        catch (Exception ex)
        {
            return Result.Failure<StoryTask>(ex.Message);
        }
    }

    public Result UpdateTask(Guid taskId, string title, string? notes)
    {
        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        try
        {
            located.Value.Task.UpdateDetails(title, notes);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Moves a task to a target step and lane at the given order.
    /// </summary>
    public Result MoveTask(Guid taskId, Guid targetStepId, Guid targetLaneId, int newOrder)
    {
        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        var targetStepResult = LocateStep(targetStepId);
        if (targetStepResult.IsFailure)
            return Result.Failure(targetStepResult.Error);

        if (_lanes.All(x => x.Id != targetLaneId))
            return Result.Failure("Lane does not exist on this story map.");

        var (sourceStep, task) = located.Value;
        var targetStep = targetStepResult.Value.Step;

        var removeResult = sourceStep.RemoveTask(taskId);
        if (removeResult.IsFailure)
            return Result.Failure(removeResult.Error);

        task.MoveTo(targetStep.Id, targetLaneId, targetStep.NextTaskOrder(targetLaneId));
        targetStep.AttachTask(task);

        // Place the task at the requested order within its (step, lane) cell.
        var cell = targetStep.Tasks.Where(x => x.LaneId == targetLaneId).ToList();
        Reorder(cell, task, newOrder, x => x.SortOrder, (x, o) => x.SetSortOrder(o));
        return Result.Success();
    }

    public Result DeleteTask(Guid taskId)
    {
        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        var removeResult = located.Value.Step.RemoveTask(taskId);
        return removeResult.IsFailure ? Result.Failure(removeResult.Error) : Result.Success();
    }

    public Result SetTaskPersonas(Guid taskId, IEnumerable<Guid> personaIds)
    {
        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        var validationResult = ValidatePersonaIds(personaIds, out var validated);
        if (validationResult.IsFailure)
            return validationResult;

        located.Value.Task.SetPersonas(validated);
        return Result.Success();
    }

    private Result<(Step Step, StoryTask Task)> LocateTask(Guid taskId)
    {
        foreach (var goal in _goals)
        {
            foreach (var step in goal.Steps)
            {
                var taskResult = step.GetTask(taskId);
                if (taskResult.IsSuccess)
                    return (step, taskResult.Value);
            }
        }
        return Result.Failure<(Step, StoryTask)>("Task does not exist on this story map.");
    }

    #endregion Tasks

    #region Checklist

    public Result<ChecklistItem> AddChecklistItem(Guid taskId, string name)
    {
        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure<ChecklistItem>(located.Error);

        return located.Value.Task.AddChecklistItem(name);
    }

    public Result RenameChecklistItem(Guid taskId, Guid itemId, string name)
    {
        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        return located.Value.Task.RenameChecklistItem(itemId, name);
    }

    public Result SetChecklistItemChecked(Guid taskId, Guid itemId, bool isChecked)
    {
        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        return located.Value.Task.SetChecklistItemChecked(itemId, isChecked);
    }

    public Result RemoveChecklistItem(Guid taskId, Guid itemId)
    {
        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        return located.Value.Task.RemoveChecklistItem(itemId);
    }

    /// <summary>
    /// Promotes a checklist item into a task in the same step, landing in the default lane.
    /// </summary>
    public Result<StoryTask> PromoteChecklistItem(Guid taskId, Guid itemId)
    {
        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure<StoryTask>(located.Error);

        var (step, task) = located.Value;
        var promoteResult = task.PromoteChecklistItem(itemId);
        if (promoteResult.IsFailure)
            return Result.Failure<StoryTask>(promoteResult.Error);

        try
        {
            return step.AddTask(DefaultLane.Id, promoteResult.Value);
        }
        catch (Exception ex)
        {
            return Result.Failure<StoryTask>(ex.Message);
        }
    }

    #endregion Checklist

    #region Work item link

    /// <summary>
    /// Links a task to an existing work item. A work item can be linked to at most one task per map.
    /// </summary>
    public Result LinkWorkItem(Guid taskId, int workItemId)
    {
        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        var alreadyLinked = _goals
            .SelectMany(g => g.Steps)
            .SelectMany(s => s.Tasks)
            .Any(t => t.Id != taskId && t.LinkedWorkItemId == workItemId);

        if (alreadyLinked)
            return Result.Failure("That work item is already linked to another task on this map.");

        located.Value.Task.LinkWorkItem(workItemId);
        return Result.Success();
    }

    public Result UnlinkWorkItem(Guid taskId)
    {
        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        located.Value.Task.UnlinkWorkItem();
        return Result.Success();
    }

    #endregion Work item link

    #region Lanes

    /// <summary>
    /// Adds a lane, appended below the existing ones.
    /// </summary>
    public Result<SwimLane> AddLane(string name)
    {
        try
        {
            int nextOrder = _lanes.Count > 0 ? _lanes.Max(x => x.SortOrder) + 1 : 0;
            var lane = new SwimLane(Id, name, nextOrder, isDefault: false);
            _lanes.Add(lane);
            return lane;
        }
        catch (Exception ex)
        {
            return Result.Failure<SwimLane>(ex.Message);
        }
    }

    public Result RenameLane(Guid laneId, string name)
    {
        var laneResult = GetLane(laneId);
        if (laneResult.IsFailure)
            return Result.Failure(laneResult.Error);

        return laneResult.Value.Rename(name);
    }

    public Result SetLaneDates(Guid laneId, LocalDate? startDate, LocalDate? endDate)
    {
        var laneResult = GetLane(laneId);
        if (laneResult.IsFailure)
            return Result.Failure(laneResult.Error);

        laneResult.Value.SetDates(startDate, endDate);
        return Result.Success();
    }

    /// <summary>
    /// Reorders a lane. The default lane cannot be moved, and no lane can be moved above it.
    /// </summary>
    public Result ReorderLane(Guid laneId, int newOrder)
    {
        var laneResult = GetLane(laneId);
        if (laneResult.IsFailure)
            return Result.Failure(laneResult.Error);

        var lane = laneResult.Value;
        if (lane.IsDefault)
            return Result.Failure("The default lane cannot be reordered.");

        // The default lane always holds order 0, so non-default lanes occupy positions 1..n.
        var clampedOrder = Math.Max(1, newOrder);
        Reorder(_lanes, lane, clampedOrder, x => x.SortOrder, (x, o) => x.SetSortOrder(o));
        NormalizeLaneOrder();
        return Result.Success();
    }

    /// <summary>
    /// Removes a lane. Its tasks are not deleted — they return to the default lane. Returns the
    /// number of tasks that were moved.
    /// </summary>
    public Result<int> RemoveLane(Guid laneId)
    {
        var laneResult = GetLane(laneId);
        if (laneResult.IsFailure)
            return Result.Failure<int>(laneResult.Error);

        var lane = laneResult.Value;
        if (lane.IsDefault)
            return Result.Failure<int>("The default lane cannot be removed.");

        var defaultLaneId = DefaultLane.Id;
        int movedCount = 0;
        foreach (var step in _goals.SelectMany(g => g.Steps))
        {
            movedCount += step.ReassignTasksToLane(lane.Id, defaultLaneId);
        }

        _lanes.Remove(lane);
        NormalizeLaneOrder();
        return movedCount;
    }

    private Result<SwimLane> GetLane(Guid laneId)
    {
        var lane = _lanes.FirstOrDefault(x => x.Id == laneId);
        return lane is not null
            ? lane
            : Result.Failure<SwimLane>("Lane does not exist on this story map.");
    }

    /// <summary>
    /// Keeps the default lane at order 0 and renumbers the remaining lanes 1..n in their current
    /// relative order.
    /// </summary>
    private void NormalizeLaneOrder()
    {
        DefaultLane.SetSortOrder(0);
        int i = 1;
        foreach (var lane in _lanes.Where(x => !x.IsDefault).OrderBy(x => x.SortOrder).ToList())
        {
            lane.SetSortOrder(i);
            i++;
        }
    }

    #endregion Lanes

    #region Personas

    /// <summary>
    /// Defines a persona on the map.
    /// </summary>
    public Result<Persona> AddPersona(string name, string? description, string color)
    {
        try
        {
            var persona = new Persona(Id, name, description, color);
            _personas.Add(persona);
            return persona;
        }
        catch (Exception ex)
        {
            return Result.Failure<Persona>(ex.Message);
        }
    }

    public Result UpdatePersona(Guid personaId, string name, string? description, string color)
    {
        var persona = _personas.FirstOrDefault(x => x.Id == personaId);
        if (persona is null)
            return Result.Failure("Persona does not exist on this story map.");

        try
        {
            persona.Update(name, description, color);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Deletes a persona and strips its tag from every goal, step, and task. Returns the number of
    /// nodes the persona was tagged on, for the confirmation message.
    /// </summary>
    public Result<int> DeletePersona(Guid personaId)
    {
        var persona = _personas.FirstOrDefault(x => x.Id == personaId);
        if (persona is null)
            return Result.Failure<int>("Persona does not exist on this story map.");

        int taggedNodes = 0;
        foreach (var goal in _goals)
        {
            if (goal.PersonaIds.Contains(personaId))
            {
                goal.RemovePersona(personaId);
                taggedNodes++;
            }

            foreach (var step in goal.Steps)
            {
                if (step.PersonaIds.Contains(personaId))
                {
                    step.RemovePersona(personaId);
                    taggedNodes++;
                }

                foreach (var task in step.Tasks)
                {
                    if (task.PersonaIds.Contains(personaId))
                    {
                        task.RemovePersona(personaId);
                        taggedNodes++;
                    }
                }
            }
        }

        _personas.Remove(persona);
        return taggedNodes;
    }

    /// <summary>
    /// The number of goals, steps, and tasks a persona is tagged on. Shown in the manage-personas
    /// dialog.
    /// </summary>
    public int CountPersonaTags(Guid personaId) =>
        _goals.Count(g => g.PersonaIds.Contains(personaId))
        + _goals.SelectMany(g => g.Steps).Count(s => s.PersonaIds.Contains(personaId))
        + _goals.SelectMany(g => g.Steps).SelectMany(s => s.Tasks).Count(t => t.PersonaIds.Contains(personaId));

    public Result SetGoalPersonas(Guid goalId, IEnumerable<Guid> personaIds)
    {
        var goalResult = GetGoal(goalId);
        if (goalResult.IsFailure)
            return Result.Failure(goalResult.Error);

        var validationResult = ValidatePersonaIds(personaIds, out var validated);
        if (validationResult.IsFailure)
            return validationResult;

        goalResult.Value.SetPersonas(validated);
        return Result.Success();
    }

    public Result SetStepPersonas(Guid stepId, IEnumerable<Guid> personaIds)
    {
        var located = LocateStep(stepId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        var validationResult = ValidatePersonaIds(personaIds, out var validated);
        if (validationResult.IsFailure)
            return validationResult;

        located.Value.Step.SetPersonas(validated);
        return Result.Success();
    }

    private Result ValidatePersonaIds(IEnumerable<Guid> personaIds, out List<Guid> validated)
    {
        validated = personaIds.Distinct().ToList();
        var unknown = validated.Where(id => _personas.All(p => p.Id != id)).ToList();
        return unknown.Count > 0
            ? Result.Failure("One or more personas do not exist on this story map.")
            : Result.Success();
    }

    #endregion Personas

    #region Ordering helpers

    /// <summary>
    /// Moves <paramref name="item"/> to <paramref name="newOrder"/> within <paramref name="items"/>
    /// and renumbers the collection contiguously from 0.
    /// </summary>
    private static void Reorder<T>(List<T> items, T item, int newOrder, Func<T, int> getOrder, Action<T, int> setOrder)
    {
        var ordered = items.OrderBy(getOrder).ToList();
        ordered.Remove(item);
        var clamped = Math.Clamp(newOrder, 0, ordered.Count);
        ordered.Insert(clamped, item);

        int i = 0;
        foreach (var element in ordered)
        {
            setOrder(element, i);
            i++;
        }
    }

    private static void Renumber<T>(IEnumerable<T> items, Func<T, int> getOrder, Action<T, int> setOrder)
    {
        int i = 0;
        foreach (var element in items.OrderBy(getOrder).ToList())
        {
            setOrder(element, i);
            i++;
        }
    }

    private static List<Step> MutableSteps(Goal goal) => goal.Steps.ToList();

    #endregion Ordering helpers

    /// <summary>
    /// Creates a new Story Map. The map lands with one placeholder goal (with one step) and the
    /// single default lane already in place — an empty grid gives people nothing to react to.
    /// </summary>
    public static Result<StoryMap> Create(string name, string? description, string ownerId, string firstGoalName, string firstStepName)
    {
        try
        {
            var map = new StoryMap(name, description, ownerId);
            map._lanes.Add(SwimLane.CreateDefault(map.Id));
            var goalResult = map.AddGoal(firstGoalName, firstStepName);
            if (goalResult.IsFailure)
                return Result.Failure<StoryMap>(goalResult.Error);

            return map;
        }
        catch (Exception ex)
        {
            return Result.Failure<StoryMap>(ex.Message);
        }
    }
}
