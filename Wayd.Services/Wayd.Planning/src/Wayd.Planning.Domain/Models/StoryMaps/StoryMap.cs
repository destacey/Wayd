using System.Linq.Expressions;
using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Interfaces;
using NodaTime;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Domain.Models.StoryMaps;

/// <summary>
/// A Story Map — a persistent surface for describing what a product should do, organized the way
/// story mapping is practiced: a horizontal narrative of goals sliced vertically into what gets
/// built when. This is the aggregate root; goals, steps, tasks, swim lanes, and personas are all owned
/// by it and mutated only through it.
/// </summary>
/// <remarks>
/// The map decides who may do what. Owners, and holders of the Story Maps Administer permission, manage it: its
/// details, lifecycle, owners, editors, visibility and edit access. Content changes need <see cref="CanEdit"/>.
/// </remarks>
public sealed class StoryMap : BaseSoftDeletableEntity, IHasIdAndKey
{
    /// <summary>The refusal for an actor who cannot see the map, worded so a private map's existence is not disclosed.</summary>
    public const string NotFoundError = "Story map not found.";

    /// <summary>The refusal for an actor who can see the map but does not own it.</summary>
    public const string NotAnOwnerError = "Only the story map's owners can do this.";

    /// <summary>The refusal for an actor who can see the map but cannot change its content.</summary>
    public const string NotAnEditorError = "Only the story map's owners and editors can change it.";

    private readonly List<Goal> _goals = [];
    private readonly List<SwimLane> _swimLanes = [];
    private readonly List<Persona> _personas = [];
    private readonly List<StoryMapOwner> _owners = [];
    private readonly List<StoryMapEditor> _editors = [];
    private readonly List<StoryMapEditorTeam> _editorTeams = [];

    private StoryMap() { }

    private StoryMap(string name, string? description, Visibility visibility, StoryMapEditAccess editAccess, Guid ownerEmployeeId)
    {
        Name = name;
        Description = description;
        Visibility = visibility;
        EditAccess = editAccess;
        Status = StatusCategory.Active;
        _owners.Add(new StoryMapOwner(Id, ownerEmployeeId));
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
    /// Who can see the map: anyone with the Story Maps View permission, or only its owners and editors.
    /// </summary>
    public Visibility Visibility { get; private set; }

    /// <summary>
    /// Who can change a public map's content. A private map admits only its owners and editors, so there it has no
    /// effect.
    /// </summary>
    public StoryMapEditAccess EditAccess { get; private set; }

    /// <summary>
    /// The employees who own the map. Changing them requires at least one; a map migrated from an owner with no
    /// linked employee has none, and only a Story Maps administrator can manage it.
    /// </summary>
    public IReadOnlyCollection<StoryMapOwner> Owners => _owners.AsReadOnly();

    /// <summary>The employees invited to edit the map.</summary>
    public IReadOnlyCollection<StoryMapEditor> Editors => _editors.AsReadOnly();

    /// <summary>The teams whose current members are invited to edit the map.</summary>
    public IReadOnlyCollection<StoryMapEditorTeam> EditorTeams => _editorTeams.AsReadOnly();

    /// <summary>
    /// The lifecycle status of the map. Active while in use, Removed once archived.
    /// </summary>
    public StatusCategory Status { get; private set; }

    /// <summary>
    /// Optimistic concurrency token for the whole aggregate. Everything the map owns is mutated
    /// through this root, so guarding the root guards the graph.
    /// </summary>
    public byte[]? Version { get; private set; }

    /// <summary>
    /// The goals on the map, in order (the top-row narrative).
    /// </summary>
    public IReadOnlyList<Goal> Goals => [.. _goals.OrderBy(x => x.Order)];

    /// <summary>
    /// The swim lanes on the map, in order. The default swim lane is always first.
    /// </summary>
    public IReadOnlyList<SwimLane> SwimLanes => [.. _swimLanes.OrderBy(x => x.Order)];

    /// <summary>
    /// The personas defined on the map.
    /// </summary>
    public IReadOnlyList<Persona> Personas => [.. _personas.OrderBy(x => x.Order)];

    /// <summary>
    /// The lane every map is created with. Throws if a map was materialised without one (a hand-built
    /// fixture, or data predating the invariant); callers on a <see cref="Result"/>-returning path use
    /// <see cref="TryGetDefaultSwimLane"/> instead.
    /// </summary>
    private SwimLane DefaultSwimLane => _swimLanes.Single(x => x.IsDefault);

    private Result<SwimLane> TryGetDefaultSwimLane()
    {
        var lane = _swimLanes.FirstOrDefault(x => x.IsDefault);
        return lane is not null
            ? Result.Success(lane)
            : Result.Failure<SwimLane>("This story map has no default swim lane.");
    }

    #region Map lifecycle

    /// <summary>
    /// Archiving is terminal, so an archived map is a historical record: readable and deletable, but
    /// not editable. Every public method that changes the map or anything it contains guards on this
    /// first — the UI hides the affordances, but the API is reachable directly.
    /// </summary>
    private bool IsArchived => Status == StatusCategory.Removed;

    private const string ArchivedError = "This story map is archived and cannot be changed.";

    /// <summary>
    /// Updates the map's name and description, which its owners decide.
    /// </summary>
    public Result Update(string name, string? description, StoryMapActor actor)
    {
        var canManage = EnsureCanManage(actor);
        if (canManage.IsFailure)
            return canManage;

        if (IsArchived) return Result.Failure(ArchivedError);

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
    /// Archives the map. Only active maps can be archived.
    /// </summary>
    public Result Archive(StoryMapActor actor)
    {
        var canManage = EnsureCanManage(actor);
        if (canManage.IsFailure)
            return canManage;

        if (Status != StatusCategory.Active)
            return Result.Failure("Only active story maps can be archived.");

        Status = StatusCategory.Removed;
        return Result.Success();
    }

    /// <summary>
    /// Check that the actor may delete the map, which only its owners and Story Maps administrators can.
    /// </summary>
    public Result CanDelete(StoryMapActor actor) => EnsureCanManage(actor);

    #endregion Map lifecycle

    #region Access

    /// <summary>
    /// Whether the employee is one of the map's owners. Requires <see cref="Owners"/> to be loaded.
    /// </summary>
    public bool IsOwner(Guid employeeId) => _owners.Any(o => o.EmployeeId == employeeId);

    /// <summary>
    /// Whether the actor is an editor, directly or through an editor team. Requires <see cref="Editors"/> and
    /// <see cref="EditorTeams"/> to be loaded.
    /// </summary>
    public bool IsEditor(StoryMapActor actor)
    {
        Guard.Against.Null(actor, nameof(actor));

        return _editors.Any(e => e.EmployeeId == actor.EmployeeId)
            || _editorTeams.Any(t => actor.TeamIds.Contains(t.TeamId));
    }

    /// <summary>
    /// Whether the actor manages the map: an owner, or a Story Maps administrator. Requires <see cref="Owners"/>
    /// to be loaded.
    /// </summary>
    public bool CanManage(StoryMapActor actor)
    {
        Guard.Against.Null(actor, nameof(actor));
        return actor.IsAdministrator || IsOwner(actor.EmployeeId);
    }

    /// <summary>
    /// Whether the actor can see the map. Requires <see cref="Owners"/>, <see cref="Editors"/> and
    /// <see cref="EditorTeams"/> to be loaded, or a private map reads as closed to its editors.
    /// <see cref="AccessibleTo"/> is the same rule for a query.
    /// </summary>
    public bool CanAccess(StoryMapActor actor) =>
        Visibility == Visibility.Public || CanManage(actor) || IsEditor(actor);

    /// <summary>
    /// Whether the actor can change the map's content: its owners and editors, and on a public map open to anyone
    /// who can view it, everyone who can see it. Says nothing about whether the map is archived. Requires the same
    /// collections as <see cref="CanAccess"/>.
    /// </summary>
    public bool CanEdit(StoryMapActor actor) =>
        (Visibility == Visibility.Public && EditAccess == StoryMapEditAccess.AnyoneWhoCanView)
        || CanManage(actor)
        || IsEditor(actor);

    /// <summary>
    /// <see cref="CanAccess"/> as a query filter, for a reader who may have no employee.
    /// </summary>
    /// <param name="employeeId">The reader's employee, or null when the account has none.</param>
    /// <param name="isAdministrator">Whether the reader holds the Story Maps Administer permission.</param>
    /// <param name="teamIds">The teams the reader belongs to, among those any map names as editors.</param>
    public static Expression<Func<StoryMap, bool>> AccessibleTo(Guid? employeeId, bool isAdministrator, IReadOnlyCollection<Guid> teamIds)
    {
        Guard.Against.Null(teamIds, nameof(teamIds));

        if (isAdministrator)
            return m => true;

        if (employeeId is null)
            return m => m.Visibility == Visibility.Public;

        var id = employeeId.Value;
        return m => m.Visibility == Visibility.Public
            || m.Owners.Any(o => o.EmployeeId == id)
            || m.Editors.Any(e => e.EmployeeId == id)
            || m.EditorTeams.Any(t => teamIds.Contains(t.TeamId));
    }

    /// <summary>
    /// <see cref="CanEdit"/> as a query filter, for an actor's employee, administrator status and teams.
    /// </summary>
    /// <param name="teamIds">The teams the actor belongs to, among those any map names as editors.</param>
    public static Expression<Func<StoryMap, bool>> EditableBy(Guid employeeId, bool isAdministrator, IReadOnlyCollection<Guid> teamIds)
    {
        Guard.Against.Null(teamIds, nameof(teamIds));

        if (isAdministrator)
            return m => true;

        return m => (m.Visibility == Visibility.Public && m.EditAccess == StoryMapEditAccess.AnyoneWhoCanView)
            || m.Owners.Any(o => o.EmployeeId == employeeId)
            || m.Editors.Any(e => e.EmployeeId == employeeId)
            || m.EditorTeams.Any(t => teamIds.Contains(t.TeamId));
    }

    /// <summary>
    /// Change who can see the map. Allowed on an archived map, which can still be shared or made private. A
    /// connection that loses access is not removed here; the caller must evict it.
    /// </summary>
    public Result SetVisibility(Visibility visibility, StoryMapActor actor)
    {
        var canManage = EnsureCanManage(actor);
        if (canManage.IsFailure)
            return canManage;

        Visibility = visibility;
        return Result.Success();
    }

    /// <summary>
    /// Change who can edit a public map's content. Allowed on an archived map.
    /// </summary>
    public Result SetEditAccess(StoryMapEditAccess editAccess, StoryMapActor actor)
    {
        var canManage = EnsureCanManage(actor);
        if (canManage.IsFailure)
            return canManage;

        EditAccess = editAccess;
        return Result.Success();
    }

    /// <summary>
    /// Replace the map's owners. At least one is required. Allowed on an archived map.
    /// </summary>
    public Result SetOwners(IEnumerable<Guid> employeeIds, StoryMapActor actor)
    {
        Guard.Against.Null(employeeIds, nameof(employeeIds));

        var canManage = EnsureCanManage(actor);
        if (canManage.IsFailure)
            return canManage;

        var ids = employeeIds.Distinct().ToHashSet();
        if (ids.Count == 0)
            return Result.Failure("A story map needs at least one owner.");

        _owners.RemoveAll(o => !ids.Contains(o.EmployeeId));
        foreach (var id in ids.Where(id => !IsOwner(id)))
            _owners.Add(new StoryMapOwner(Id, id));

        return Result.Success();
    }

    /// <summary>
    /// Replace the employees and teams invited to edit the map. Allowed on an archived map.
    /// </summary>
    public Result SetEditors(IEnumerable<Guid> employeeIds, IEnumerable<Guid> teamIds, StoryMapActor actor)
    {
        Guard.Against.Null(employeeIds, nameof(employeeIds));
        Guard.Against.Null(teamIds, nameof(teamIds));

        var canManage = EnsureCanManage(actor);
        if (canManage.IsFailure)
            return canManage;

        var employees = employeeIds.Distinct().ToHashSet();
        _editors.RemoveAll(e => !employees.Contains(e.EmployeeId));
        foreach (var id in employees.Where(id => !_editors.Any(e => e.EmployeeId == id)))
            _editors.Add(new StoryMapEditor(Id, id));

        var teams = teamIds.Distinct().ToHashSet();
        _editorTeams.RemoveAll(t => !teams.Contains(t.TeamId));
        foreach (var id in teams.Where(id => !_editorTeams.Any(t => t.TeamId == id)))
            _editorTeams.Add(new StoryMapEditorTeam(Id, id));

        return Result.Success();
    }

    /// <summary>
    /// Refuses an actor who does not manage the map: as not found when they cannot see it, otherwise as not an owner.
    /// </summary>
    private Result EnsureCanManage(StoryMapActor actor)
    {
        if (CanManage(actor))
            return Result.Success();

        return CanAccess(actor)
            ? Result.Failure(NotAnOwnerError)
            : Result.Failure(NotFoundError);
    }

    /// <summary>
    /// Refuses a content change the actor may not make, or one to an archived map. Access is checked first, so a
    /// private map's state is not disclosed to someone who cannot see it.
    /// </summary>
    private Result EnsureCanEdit(StoryMapActor actor)
    {
        if (!CanAccess(actor))
            return Result.Failure(NotFoundError);

        if (!CanEdit(actor))
            return Result.Failure(NotAnEditorError);

        return IsArchived ? Result.Failure(ArchivedError) : Result.Success();
    }

    #endregion Access

    #region Goals

    /// <summary>
    /// Adds a goal to the map. The goal starts empty; steps are added to it separately.
    /// </summary>
    public Result<Goal> AddGoal(string name, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return Result.Failure<Goal>(denied.Error);

        try
        {
            int nextOrder = _goals.Count > 0 ? _goals.Max(x => x.Order) + 1 : 0;
            var goal = new Goal(Id, name, nextOrder);
            _goals.Add(goal);
            return goal;
        }
        catch (Exception ex)
        {
            return Result.Failure<Goal>(ex.Message);
        }
    }

    public Result RenameGoal(Guid goalId, string name, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

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
    public Result ReorderGoal(Guid goalId, int newOrder, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

        var goalResult = GetGoal(goalId);
        if (goalResult.IsFailure)
            return Result.Failure(goalResult.Error);

        Reorder(_goals, goalResult.Value, newOrder, x => x.Order, (x, o) => x.SetOrder(o));
        return Result.Success();
    }

    /// <summary>
    /// Deletes a goal, along with its steps and their tasks. Deleting the last goal returns the map
    /// to its empty state, where the user is prompted to add a new goal.
    /// </summary>
    public Result DeleteGoal(Guid goalId, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

        var goalResult = GetGoal(goalId);
        if (goalResult.IsFailure)
            return Result.Failure(goalResult.Error);

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

    private void ResetGoalOrder() => Renumber(_goals, x => x.Order, (x, o) => x.SetOrder(o));

    #endregion Goals

    #region Steps

    /// <summary>
    /// Adds a step to a goal.
    /// </summary>
    public Result<Step> AddStep(Guid goalId, string name, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return Result.Failure<Step>(denied.Error);

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

    public Result RenameStep(Guid stepId, string name, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

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
    public Result ReorderStep(Guid stepId, int newOrder, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

        var located = LocateStep(stepId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        var (goal, step) = located.Value;
        var stepsField = MutableSteps(goal);
        Reorder(stepsField, step, newOrder, x => x.Order, (x, o) => x.SetOrder(o));
        return Result.Success();
    }

    /// <summary>
    /// Moves a step into a different goal at the given order. The source goal may be left empty.
    /// </summary>
    public Result MoveStep(Guid stepId, Guid targetGoalId, int newOrder, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

        var located = LocateStep(stepId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        var (sourceGoal, step) = located.Value;
        if (sourceGoal.Id == targetGoalId)
            return ReorderStep(stepId, newOrder, actor);

        var targetGoalResult = GetGoal(targetGoalId);
        if (targetGoalResult.IsFailure)
            return Result.Failure(targetGoalResult.Error);

        var targetGoal = targetGoalResult.Value;

        var detachResult = sourceGoal.DetachStep(step);
        if (detachResult.IsFailure)
            return detachResult;

        step.ChangeGoal(targetGoal.Id, targetGoal.NextStepOrder());
        targetGoal.AttachStep(step);
        targetGoal.ResetStepOrder();

        // Place the step at the requested order within the target goal.
        Reorder(MutableSteps(targetGoal), step, newOrder, x => x.Order, (x, o) => x.SetOrder(o));
        return Result.Success();
    }

    /// <summary>
    /// Deletes a step and its tasks. A goal may be left with no steps.
    /// </summary>
    public Result DeleteStep(Guid stepId, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

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
    /// Adds a task to a step. Without a swim lane, the task lands in the default swim lane.
    /// </summary>
    public Result<StoryMapTask> AddTask(Guid stepId, string title, Guid? laneId, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return Result.Failure<StoryMapTask>(denied.Error);

        var located = LocateStep(stepId);
        if (located.IsFailure)
            return Result.Failure<StoryMapTask>(located.Error);

        SwimLane? lane;
        if (laneId is null)
        {
            var defaultLane = TryGetDefaultSwimLane();
            if (defaultLane.IsFailure)
                return Result.Failure<StoryMapTask>(defaultLane.Error);
            lane = defaultLane.Value;
        }
        else
        {
            lane = _swimLanes.FirstOrDefault(x => x.Id == laneId.Value);
        }

        if (lane is null)
            return Result.Failure<StoryMapTask>("Swim lane does not exist on this story map.");

        try
        {
            return located.Value.Step.AddTask(lane.Id, title);
        }
        catch (Exception ex)
        {
            return Result.Failure<StoryMapTask>(ex.Message);
        }
    }

    /// <summary>
    /// Sets a task's title and description together.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="RenameTask"/> or <see cref="SetTaskDescription"/> when only one field is
    /// being edited: this overwrites both, so sending the untouched field back reverts a concurrent
    /// edit to it. Kept for API consumers setting both at once.
    /// </remarks>
    public Result UpdateTask(Guid taskId, string title, string? description, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        try
        {
            located.Value.Task.UpdateDetails(title, description);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Renames a task, leaving its description untouched.
    /// </summary>
    public Result RenameTask(Guid taskId, string title, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        try
        {
            located.Value.Task.Rename(title);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Sets a task's description, leaving its title untouched.
    /// </summary>
    public Result SetTaskDescription(Guid taskId, string? description, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        try
        {
            located.Value.Task.SetDescription(description);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Moves a task to a target step and swim lane at the given order.
    /// </summary>
    public Result MoveTask(Guid taskId, Guid targetStepId, Guid targetSwimLaneId, int newOrder, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        var targetStepResult = LocateStep(targetStepId);
        if (targetStepResult.IsFailure)
            return Result.Failure(targetStepResult.Error);

        if (_swimLanes.All(x => x.Id != targetSwimLaneId))
            return Result.Failure("Swim lane does not exist on this story map.");

        var (sourceStep, task) = located.Value;
        var targetStep = targetStepResult.Value.Step;

        var removeResult = sourceStep.RemoveTask(taskId);
        if (removeResult.IsFailure)
            return Result.Failure(removeResult.Error);

        task.MoveTo(targetStep.Id, targetSwimLaneId, targetStep.NextTaskOrder(targetSwimLaneId));
        targetStep.AttachTask(task);

        // Place the task at the requested order within its (step, swim lane) cell.
        var cell = targetStep.Tasks.Where(x => x.SwimLaneId == targetSwimLaneId).ToList();
        Reorder(cell, task, newOrder, x => x.Order, (x, o) => x.SetOrder(o));
        return Result.Success();
    }

    public Result DeleteTask(Guid taskId, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        var removeResult = located.Value.Step.RemoveTask(taskId);
        return removeResult.IsFailure ? Result.Failure(removeResult.Error) : Result.Success();
    }

    public Result SetTaskPersonas(Guid taskId, IEnumerable<Guid> personaIds, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        var validationResult = ValidatePersonaIds(personaIds, out var validated);
        if (validationResult.IsFailure)
            return validationResult;

        located.Value.Task.SetPersonas(validated);
        return Result.Success();
    }

    private Result<(Step Step, StoryMapTask Task)> LocateTask(Guid taskId)
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
        return Result.Failure<(Step, StoryMapTask)>("Task does not exist on this story map.");
    }

    #endregion Tasks

    #region Checklist

    public Result<ChecklistItem> AddChecklistItem(Guid taskId, string name, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return Result.Failure<ChecklistItem>(denied.Error);

        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure<ChecklistItem>(located.Error);

        return located.Value.Task.AddChecklistItem(name);
    }

    public Result RenameChecklistItem(Guid taskId, Guid itemId, string name, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        return located.Value.Task.RenameChecklistItem(itemId, name);
    }

    public Result SetChecklistItemChecked(Guid taskId, Guid itemId, bool isChecked, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        return located.Value.Task.SetChecklistItemChecked(itemId, isChecked);
    }

    public Result RemoveChecklistItem(Guid taskId, Guid itemId, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        return located.Value.Task.RemoveChecklistItem(itemId);
    }

    /// <summary>
    /// Promotes a checklist item into a task in the same step, landing in the default swim lane.
    /// </summary>
    public Result<StoryMapTask> PromoteChecklistItem(Guid taskId, Guid itemId, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return Result.Failure<StoryMapTask>(denied.Error);

        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure<StoryMapTask>(located.Error);

        var (step, task) = located.Value;

        // Resolve the destination before promoting: PromoteChecklistItem removes the item, so
        // failing after it would lose the item without producing the task it became.
        var defaultLane = TryGetDefaultSwimLane();
        if (defaultLane.IsFailure)
            return Result.Failure<StoryMapTask>(defaultLane.Error);

        var promoteResult = task.PromoteChecklistItem(itemId);
        if (promoteResult.IsFailure)
            return Result.Failure<StoryMapTask>(promoteResult.Error);

        try
        {
            return step.AddTask(defaultLane.Value.Id, promoteResult.Value);
        }
        catch (Exception ex)
        {
            return Result.Failure<StoryMapTask>(ex.Message);
        }
    }

    #endregion Checklist

    #region Work item link

    /// <summary>
    /// Links a task to an existing work item. A work item can be linked to at most one task per map.
    /// </summary>
    public Result LinkWorkItem(Guid taskId, int workItemId, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

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

    public Result UnlinkWorkItem(Guid taskId, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

        var located = LocateTask(taskId);
        if (located.IsFailure)
            return Result.Failure(located.Error);

        located.Value.Task.UnlinkWorkItem();
        return Result.Success();
    }

    #endregion Work item link

    #region Swim lanes

    /// <summary>
    /// Adds a swim lane, appended below the existing ones.
    /// </summary>
    public Result<SwimLane> AddSwimLane(string name, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return Result.Failure<SwimLane>(denied.Error);

        try
        {
            int nextOrder = _swimLanes.Count > 0 ? _swimLanes.Max(x => x.Order) + 1 : 0;
            var lane = new SwimLane(Id, name, nextOrder, isDefault: false);
            _swimLanes.Add(lane);
            return lane;
        }
        catch (Exception ex)
        {
            return Result.Failure<SwimLane>(ex.Message);
        }
    }

    public Result RenameSwimLane(Guid laneId, string name, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

        var laneResult = GetSwimLane(laneId);
        if (laneResult.IsFailure)
            return Result.Failure(laneResult.Error);

        return laneResult.Value.Rename(name);
    }

    public Result SetSwimLaneDates(Guid laneId, LocalDate? startDate, LocalDate? endDate, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

        var laneResult = GetSwimLane(laneId);
        if (laneResult.IsFailure)
            return Result.Failure(laneResult.Error);

        laneResult.Value.SetDates(startDate, endDate);
        return Result.Success();
    }

    /// <summary>
    /// Reorders a swim lane. The default swim lane cannot be moved, and no swim lane can be moved above it.
    /// </summary>
    public Result ReorderSwimLane(Guid laneId, int newOrder, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

        var laneResult = GetSwimLane(laneId);
        if (laneResult.IsFailure)
            return Result.Failure(laneResult.Error);

        var lane = laneResult.Value;
        if (lane.IsDefault)
            return Result.Failure("The default lane cannot be reordered.");

        // The default swim lane always holds order 0, so non-default swim lanes occupy positions 1..n.
        var clampedOrder = Math.Max(1, newOrder);
        Reorder(_swimLanes, lane, clampedOrder, x => x.Order, (x, o) => x.SetOrder(o));
        NormalizeSwimLaneOrder();
        return Result.Success();
    }

    /// <summary>
    /// Removes a swim lane. Its tasks are not deleted — they return to the default swim lane. Returns the
    /// number of tasks that were moved.
    /// </summary>
    public Result<int> RemoveSwimLane(Guid laneId, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return Result.Failure<int>(denied.Error);

        var laneResult = GetSwimLane(laneId);
        if (laneResult.IsFailure)
            return Result.Failure<int>(laneResult.Error);

        var lane = laneResult.Value;
        if (lane.IsDefault)
            return Result.Failure<int>("The default lane cannot be removed.");

        var defaultLane = TryGetDefaultSwimLane();
        if (defaultLane.IsFailure)
            return Result.Failure<int>(defaultLane.Error);

        var defaultSwimLaneId = defaultLane.Value.Id;
        int movedCount = 0;
        foreach (var step in _goals.SelectMany(g => g.Steps))
        {
            movedCount += step.ReassignTasksToSwimLane(lane.Id, defaultSwimLaneId);
        }

        _swimLanes.Remove(lane);
        NormalizeSwimLaneOrder();
        return movedCount;
    }

    private Result<SwimLane> GetSwimLane(Guid laneId)
    {
        var lane = _swimLanes.FirstOrDefault(x => x.Id == laneId);
        return lane is not null
            ? lane
            : Result.Failure<SwimLane>("Swim lane does not exist on this story map.");
    }

    /// <summary>
    /// Keeps the default swim lane at order 0 and renumbers the remaining swim lanes 1..n in their current
    /// relative order.
    /// </summary>
    private void NormalizeSwimLaneOrder()
    {
        DefaultSwimLane.SetOrder(0);
        int i = 1;
        foreach (var lane in _swimLanes.Where(x => !x.IsDefault).OrderBy(x => x.Order).ToList())
        {
            lane.SetOrder(i);
            i++;
        }
    }

    #endregion Swim lanes

    #region Personas

    /// <summary>
    /// Defines a persona on the map.
    /// </summary>
    public Result<Persona> AddPersona(string name, string? description, string color, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return Result.Failure<Persona>(denied.Error);

        try
        {
            int nextOrder = _personas.Count > 0 ? _personas.Max(x => x.Order) + 1 : 0;
            var persona = new Persona(Id, name, description, color, nextOrder);
            _personas.Add(persona);
            return persona;
        }
        catch (Exception ex)
        {
            return Result.Failure<Persona>(ex.Message);
        }
    }

    public Result UpdatePersona(Guid personaId, string name, string? description, string color, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

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
    /// Reorders a persona to a new position within the map's persona list.
    /// </summary>
    public Result ReorderPersona(Guid personaId, int newOrder, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

        var persona = _personas.FirstOrDefault(x => x.Id == personaId);
        if (persona is null)
            return Result.Failure("Persona does not exist on this story map.");

        Reorder(_personas, persona, newOrder, x => x.Order, (x, o) => x.SetOrder(o));
        return Result.Success();
    }

    /// <summary>
    /// Deletes a persona and strips its tag from every goal, step, and task. Returns the number of
    /// nodes the persona was tagged on, for the confirmation message.
    /// </summary>
    public Result<int> DeletePersona(Guid personaId, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return Result.Failure<int>(denied.Error);

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
        Renumber(_personas, x => x.Order, (x, o) => x.SetOrder(o));
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

    public Result SetGoalPersonas(Guid goalId, IEnumerable<Guid> personaIds, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

        var goalResult = GetGoal(goalId);
        if (goalResult.IsFailure)
            return Result.Failure(goalResult.Error);

        var validationResult = ValidatePersonaIds(personaIds, out var validated);
        if (validationResult.IsFailure)
            return validationResult;

        goalResult.Value.SetPersonas(validated);
        return Result.Success();
    }

    public Result SetStepPersonas(Guid stepId, IEnumerable<Guid> personaIds, StoryMapActor actor)
    {
        if (EnsureCanEdit(actor) is { IsFailure: true } denied) return denied;

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
    /// Creates a new Story Map owned by its creator. The map lands empty (no goals) with only the single
    /// default swim lane in place — the user adds their first goal from the board's empty state.
    /// </summary>
    /// <param name="ownerEmployeeId">The creator, who becomes the first owner.</param>
    public static Result<StoryMap> Create(
        string name,
        string? description,
        Guid ownerEmployeeId,
        Visibility visibility = Visibility.Public,
        StoryMapEditAccess editAccess = StoryMapEditAccess.AnyoneWhoCanView)
    {
        Guard.Against.Default(ownerEmployeeId, nameof(ownerEmployeeId));

        try
        {
            var map = new StoryMap(name, description, visibility, editAccess, ownerEmployeeId);
            map._swimLanes.Add(SwimLane.CreateDefault(map.Id));
            return map;
        }
        catch (Exception ex)
        {
            return Result.Failure<StoryMap>(ex.Message);
        }
    }
}
