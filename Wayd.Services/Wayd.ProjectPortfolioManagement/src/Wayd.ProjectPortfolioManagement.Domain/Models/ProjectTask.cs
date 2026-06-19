using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Interfaces;
using NodaTime;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

/// <summary>
/// Represents a task within a project with hierarchical structure and dependency management.
/// </summary>
public sealed class ProjectTask : BaseAuditableEntity, IHasIdAndKey<ProjectTaskKey>, IHasProject
{
    private readonly List<ProjectTask> _children = [];
    private readonly HashSet<RoleAssignment<TaskRole>> _roles = [];
    private readonly List<ProjectTaskDependency> _predecessors = [];
    private readonly List<ProjectTaskDependency> _successors = [];

    private ProjectTask() { }

    private ProjectTask(
        Guid projectId,
        ProjectTaskKey key,
        string name,
        string? description,
        ProjectTaskType type,
        TaskStatus status,
        TaskPriority priority,
        Progress progress,
        int order,
        Guid? parentId,
        Guid projectPhaseId,
        FlexibleDateRange? plannedDateRange,
        LocalDate? plannedDate,
        decimal? estimatedEffortHours,
        Dictionary<TaskRole, HashSet<Guid>>? roles)
    {
        // Validation
        if (type == ProjectTaskType.Milestone && plannedDate is null)
        {
            throw new InvalidOperationException("Milestones must have a planned date.");
        }

        // No need to validate date range - FlexibleDateRange constructor handles it

        ProjectId = projectId;
        Key = key;
        Number = key.TaskNumber;
        Name = name;
        Description = description;
        Type = type;
        Status = status;
        Priority = priority;
        Progress = progress;
        Order = order;
        ParentId = parentId;
        ProjectPhaseId = projectPhaseId;
        PlannedDateRange = plannedDateRange;
        PlannedDate = plannedDate;
        EstimatedEffortHours = estimatedEffortHours;

        _roles = roles?
            .SelectMany(r => r.Value
                .Select(e => new RoleAssignment<TaskRole>(Id, r.Key, e)))
            .ToHashSet()
            ?? [];
    }

    /// <summary>
    /// The unique task key in the format {ProjectCode}-{Number} (e.g., "APOLLO-1").
    /// </summary>
    public ProjectTaskKey Key { get; private set; } = null!;

    /// <summary>
    /// The numeric part of the task key.
    /// </summary>
    public int Number { get; private init; }

    /// <summary>
    /// The ID of the project this task belongs to.
    /// </summary>
    public Guid ProjectId { get; private init; }

    /// <summary>
    /// The project this task belongs to.
    /// </summary>
    public Project Project { get; private set; } = null!;

    /// <summary>
    /// The name of the task.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// A detailed description of the task.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// The type of task (Task or Milestone).
    /// </summary>
    public ProjectTaskType Type { get; private set; }

    /// <summary>
    /// The current status of the task.
    /// </summary>
    public TaskStatus Status { get; private set; }

    /// <summary>
    /// The priority level of the task.
    /// </summary>
    public TaskPriority Priority { get; private set; }

    /// <summary>
    /// The current progress as a decimal value between 0 and 100.
    /// </summary>
    /// <remarks>A value of 0 indicates no progress, while a value of 100 indicates completion. Intermediate
    /// values represent partial progress. Milestones have a progress of 0 or 100 based on their completion status.</remarks>
    public Progress Progress { get; private set; } = null!;

    /// <summary>
    /// The order of the task within its parent (used for WBS calculation).
    /// </summary>
    public int Order { get; private set; }

    /// <summary>
    /// The ID of the parent task (null for root-level tasks).
    /// </summary>
    public Guid? ParentId { get; private set; }

    /// <summary>
    /// The parent task.
    /// </summary>
    public ProjectTask? Parent { get; private set; }

    /// <summary>
    /// The ID of the project phase this task belongs to.
    /// All tasks belong to a phase. Child tasks inherit the phase from their root ancestor.
    /// </summary>
    public Guid ProjectPhaseId { get; private set; }

    /// <summary>
    /// The project phase this task belongs to.
    /// </summary>
    public ProjectPhase? ProjectPhase { get; private set; }

    /// <summary>
    /// The child tasks.
    /// </summary>
    public IReadOnlyList<ProjectTask> Children => _children.AsReadOnly();

    /// <summary>
    /// The roles associated with the project task.
    /// </summary>
    public IReadOnlyCollection<RoleAssignment<TaskRole>> Roles => _roles;

    // Date properties for regular tasks
    /// <summary>
    /// The planned date range for the task. Only applicable for tasks (not milestones).
    /// </summary>
    public FlexibleDateRange? PlannedDateRange { get; private set; }

    // Date properties for milestones
    /// <summary>
    /// The planned date for a milestone. Only applicable for milestones (not tasks).
    /// </summary>
    public LocalDate? PlannedDate { get; private set; }

    // Effort tracking
    /// <summary>
    /// The estimated effort in hours.
    /// </summary>
    public decimal? EstimatedEffortHours { get; private set; }

    /// <summary>
    /// The dependencies where this task is the predecessor.
    /// </summary>
    public IReadOnlyList<ProjectTaskDependency> Successors => _successors.AsReadOnly();

    /// <summary>
    /// The dependencies where this task is the successor.
    /// </summary>
    public IReadOnlyList<ProjectTaskDependency> Predecessors => _predecessors.AsReadOnly();

    #region Domain Methods

    /// <summary>
    /// Updates the basic details of the task.
    /// </summary>
    public Result UpdateDetails(string name, string? description, TaskPriority priority)
    {
        Name = name;
        Description = description;
        Priority = priority;
        return Result.Success();
    }

    /// <summary>
    /// Updates the status of the task.
    /// </summary>
    public Result UpdateStatus(TaskStatus status, Instant timestamp)
    {
        if (Type == ProjectTaskType.Milestone && status == TaskStatus.InProgress)
        {
            return Result.Failure("Milestones cannot be in progress. Use NotStarted or Completed.");
        }

        Status = status;

        if (Type == ProjectTaskType.Milestone)
        {
            if (Status == TaskStatus.Completed)
            {
                Progress = Progress.Completed();
            }
            else
            {
                Progress = Progress.NotStarted();
            }
        }

        return Result.Success();
    }

    public Result UpdateProgress(Progress progress)
    {
        if (Type == ProjectTaskType.Milestone)
        {
            return Result.Failure("Milestones do not support progress updates. Use status to mark completion.");
        }

        Progress = progress;

        return Result.Success();
    }

    /// <summary>
    /// Updates the planned dates for the task.
    /// </summary>
    public Result UpdatePlannedDates(FlexibleDateRange? plannedDateRange, LocalDate? plannedDate)
    {
        if (Type == ProjectTaskType.Milestone)
        {
            if (plannedDate is null)
            {
                return Result.Failure("Milestones must have a planned date.");
            }
            PlannedDate = plannedDate;
            PlannedDateRange = null;
        }
        else
        {
            PlannedDateRange = plannedDateRange;
            PlannedDate = null;
        }

        return Result.Success();
    }

    /// <summary>
    /// Updates the effort estimates and actuals.
    /// </summary>
    public Result UpdateEffort(decimal? estimatedEffortHours)
    {
        if (estimatedEffortHours.HasValue && estimatedEffortHours < 0)
        {
            return Result.Failure("Estimated effort cannot be negative.");
        }

        EstimatedEffortHours = estimatedEffortHours;

        return Result.Success();
    }

    /// <summary>
    /// Assigns an employee to a specific role for this task.
    /// </summary>
    public Result AssignRole(TaskRole role, Guid employeeId)
    {
        return RoleManager.AssignRole(_roles, Id, role, employeeId);
    }

    /// <summary>
    /// Removes an employee from a specific role for this task.
    /// </summary>
    public Result RemoveRole(TaskRole role, Guid employeeId)
    {
        return RoleManager.RemoveAssignment(_roles, role, employeeId);
    }

    /// <summary>
    /// Updates all role assignments for this task.
    /// </summary>
    public Result UpdateRoles(Dictionary<TaskRole, HashSet<Guid>> updatedRoles)
    {
        return RoleManager.UpdateRoles(_roles, Id, updatedRoles);
    }

    /// <summary>
    /// Changes the order of this task within its parent.
    /// </summary>
    internal Result ChangeOrder(int order)
    {
        if (order < 1)
        {
            return Result.Failure("Order must be greater than 0.");
        }

        Order = order;
        return Result.Success();
    }

    /// <summary>
    /// Changes the parent of this task.
    /// </summary>
    internal Result ChangeParent(Guid? newParentId, int order)
    {
        // Validation: prevent circular references
        if (newParentId.HasValue && newParentId == Id)
        {
            return Result.Failure("A task cannot be its own parent.");
        }
        else if (newParentId.HasValue && IsDescendantOf(newParentId.Value))
        {
            return Result.Failure("A task cannot be moved under one of its descendants.");
        }

        ParentId = newParentId;

        return ChangeOrder(order);
    }

    /// <summary>
    /// Adds a dependency where this task is the predecessor.
    /// </summary>
    public Result AddDependency(ProjectTask successor)
    {
        Guard.Against.Null(successor, nameof(successor));

        if (successor.Id == Id)
        {
            return Result.Failure("A task cannot depend on itself.");
        }

        if (successor.ProjectId != ProjectId)
        {
            return Result.Failure("Dependencies can only be created between tasks in the same project.");
        }

        // Check for existing active dependency
        if (_successors.Any(d => d.SuccessorId == successor.Id && d.IsActive))
        {
            return Result.Failure("An active dependency already exists with this task.");
        }

        // TODO: Check for circular dependencies in dependency graph

        var dependency = ProjectTaskDependency.Create(Id, successor.Id, DependencyType.FinishToStart);
        _successors.Add(dependency);

        return Result.Success();
    }

    /// <summary>
    /// Removes (soft deletes) a dependency where this task is the predecessor.
    /// </summary>
    public Result RemoveDependency(Guid successorId, Instant timestamp)
    {
        var dependency = _successors.FirstOrDefault(d => d.SuccessorId == successorId && d.IsActive);
        if (dependency is null)
        {
            return Result.Failure("Active dependency does not exist.");
        }

        dependency.Remove(timestamp);
        return Result.Success();
    }

    /// <summary>
    /// Updates the task key when the owning project's key changes.
    /// Preserves the task number.
    /// </summary>
    internal Result UpdateProjectKey(ProjectKey projectKey)
    {
        Guard.Against.Null(projectKey, nameof(projectKey));

        var newKey = new ProjectTaskKey(projectKey, Number);

        if (newKey.Value == Key.Value)
        {
            return Result.Success();
        }

        Key = newKey;

        return Result.Success();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Adds a child task (called internally from parent task or project).
    /// </summary>
    internal void AddChild(ProjectTask child)
    {
        _children.Add(child);
    }

    /// <summary>
    /// Removes a child task (called internally).
    /// </summary>
    internal void RemoveChild(ProjectTask child)
    {
        _children.Remove(child);
    }

    /// <summary>
    /// Resets the order of all child tasks to eliminate gaps.
    /// </summary>
    internal void ResetChildrenOrder()
    {
        int i = 1;
        foreach (var child in _children.OrderBy(x => x.Order).ToArray())
        {
            child.Order = i;
            i++;
        }
    }

    /// <summary>
    /// Gets this task and all its descendants.
    /// </summary>
    internal List<Guid> GetSelfAndDescendants()
    {
        var ids = new List<Guid> { Id };

        foreach (var child in _children)
        {
            ids.AddRange(child.GetSelfAndDescendants());
        }

        return ids;
    }

    /// <summary>
    /// Checks if a given task ID is a descendant of this task.
    /// </summary>
    internal bool IsDescendantOf(Guid taskId)
    {
        return _children.Any(c => c.Id == taskId || c.IsDescendantOf(taskId));
    }

    #endregion

    /// <summary>
    /// Creates a new instance of a project task with the specified properties.
    /// </summary>
    /// <remarks>If progress is null, the task is initialized as not started. The method does not validate the
    /// existence of referenced project or parent task identifiers.</remarks>
    /// <param name="projectId">The unique identifier of the project to which the task belongs.</param>
    /// <param name="key">The key that uniquely identifies the task within the project.</param>
    /// <param name="name">The name of the task. Cannot be null or empty.</param>
    /// <param name="description">An optional description providing additional details about the task, or null if not specified.</param>
    /// <param name="type">The type of the task, indicating its category or purpose.</param>
    /// <param name="status">The current status of the task.</param>
    /// <param name="priority">The priority level assigned to the task.</param>
    /// <param name="progress">The progress of the task, or null to indicate not started.</param>
    /// <param name="order">The order or position of the task within its parent or list. Must be zero or greater.</param>
    /// <param name="parentId">The unique identifier of the parent task if this is a subtask; otherwise, null.</param>
    /// <param name="plannedDateRange">The planned date range for the task, or null if not specified.</param>
    /// <param name="plannedDate">The planned date for the task, or null if not specified.</param>
    /// <param name="estimatedEffortHours">The estimated effort required to complete the task, in hours, or null if not specified.</param>
    /// <param name="roles">A mapping of task roles to sets of user identifiers assigned to each role, or null if no roles are assigned.</param>
    /// <returns>A new ProjectTask instance initialized with the specified values.</returns>
    /// <summary>
    /// Changes the project phase assignment for this task.
    /// </summary>
    internal Result ChangePhase(Guid projectPhaseId)
    {
        ProjectPhaseId = projectPhaseId;
        return Result.Success();
    }

    internal static ProjectTask Create(
        Guid projectId,
        ProjectTaskKey key,
        string name,
        string? description,
        ProjectTaskType type,
        TaskStatus status,
        TaskPriority priority,
        Progress? progress,
        int order,
        Guid? parentId,
        Guid projectPhaseId,
        FlexibleDateRange? plannedDateRange,
        LocalDate? plannedDate,
        decimal? estimatedEffortHours,
        Dictionary<TaskRole, HashSet<Guid>>? roles)
    {
        return new ProjectTask(
            projectId,
            key,
            name,
            description,
            type,
            status,
            priority,
            progress ?? Progress.NotStarted(),
            order,
            parentId,
            projectPhaseId,
            plannedDateRange,
            plannedDate,
            estimatedEffortHours,
            roles);
    }
}
