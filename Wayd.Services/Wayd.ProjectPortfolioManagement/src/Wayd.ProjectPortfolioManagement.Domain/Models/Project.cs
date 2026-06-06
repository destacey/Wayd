using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.HealthChecks;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.Scoring;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models.Scoring;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using NodaTime;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

/// <summary>
/// Represents an individual project within a portfolio or program.
/// </summary>
public sealed class Project : BaseAuditableEntity, IHasIdAndKey<ProjectKey>, ISimpleProject
{
    private readonly HashSet<RoleAssignment<ProjectRole>> _roles = [];
    private readonly HashSet<StrategicThemeTag<Project>> _strategicThemeTags = [];
    private readonly HashSet<StrategicInitiativeProject> _strategicInitiativeProjects = [];
    private readonly List<ProjectPhase> _phases = [];
    private readonly List<ProjectTask> _tasks = [];
    private readonly List<ProjectHealthCheck> _healthChecks = [];
    private readonly List<ProjectScore> _scores = [];

    private Project() { }

    private Project(string name, string description, ProjectKey key, ProjectStatus status, int expenditureCategoryId, LocalDateRange? dateRange, Guid portfolioId, Guid? programId = null, string? businessCase = null, string? expectedBenefits = null, Dictionary<ProjectRole, HashSet<Guid>>? roles = null, HashSet<Guid>? strategicThemes = null)
    {
        if (Status is ProjectStatus.Active or ProjectStatus.Completed && dateRange is null)
        {
            throw new InvalidOperationException("An active and completed project must have a start and end date.");
        }

        Name = name;
        Description = description;
        Key = key;
        Status = status;
        ExpenditureCategoryId = expenditureCategoryId;
        DateRange = dateRange;
        BusinessCase = businessCase?.Trim();
        ExpectedBenefits = expectedBenefits?.Trim();

        PortfolioId = portfolioId;
        ProgramId = programId;

        _roles = roles?
            .SelectMany(r => r.Value
                .Select(e => new RoleAssignment<ProjectRole>(Id, r.Key, e)))
            .ToHashSet()
            ?? [];

        _strategicThemeTags = strategicThemes?.Select(t => new StrategicThemeTag<Project>(Id, t)).ToHashSet()
            ?? [];
    }

    /// <summary>
    /// The unique key of the project used for task key generation (e.g., "APOLLO", "MARS").
    /// This is an alternate key to the Id.
    /// Must be 2-20 uppercase alphanumeric characters or hyphens.
    /// </summary>
    public ProjectKey Key { get; private set; } = default!;

    /// <summary>
    /// The name of the project.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// A concise summary of what the project delivers and its scope.
    /// Serves as the elevator pitch — what is being built or delivered.
    /// </summary>
    public string Description
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Description)).Trim();
    } = default!;

    /// <summary>
    /// The strategic justification for the project — why it should be funded.
    /// Captures the problem being solved or opportunity being pursued, the strategic rationale,
    /// and key assumptions underpinning the investment decision.
    /// </summary>
    public string? BusinessCase { get; private set; }

    /// <summary>
    /// The specific, measurable outcomes expected upon successful delivery of the project.
    /// Examples include revenue growth, cost savings, compliance achievement, or efficiency improvements.
    /// Used to evaluate project success during and after completion.
    /// </summary>
    public string? ExpectedBenefits { get; private set; }

    /// <summary>
    /// The current status of the project.
    /// </summary>
    public ProjectStatus Status { get; private set; }

    /// <summary>
    /// Indicates if the project is in a closed state.
    /// </summary>
    public bool IsClosed => Status is ProjectStatus.Completed or ProjectStatus.Cancelled;

    /// <summary>
    /// The roles associated with the project.
    /// </summary>
    public IReadOnlyCollection<RoleAssignment<ProjectRole>> Roles => _roles;

    /// <summary>
    /// The date range defining the project's planned timeline.
    /// </summary>
    public LocalDateRange? DateRange { get; private set; }

    /// <summary>
    /// The Id of the expenditure category associated with the project.
    /// </summary>
    public int ExpenditureCategoryId { get; private set; }

    /// <summary>
    /// The expenditure category associated with the project.
    /// </summary>
    public ExpenditureCategory? ExpenditureCategory { get; private set; }

    /// <summary>
    /// The Id of the portfolio to which this project belongs.
    /// </summary>
    public Guid PortfolioId { get; private set; }

    /// <summary>
    /// The portfolio associated with this project.
    /// </summary>
    public ProjectPortfolio? Portfolio { get; private set; }

    /// <summary>
    /// The Id of the program to which this project belongs (optional).
    /// </summary>
    public Guid? ProgramId { get; private set; }

    /// <summary>
    /// The program associated with this project (if any).
    /// </summary>
    public Program? Program { get; private set; }

    /// <summary>
    /// The Id of the project lifecycle assigned to this project.
    /// Required before a project can be approved.
    /// </summary>
    public Guid? ProjectLifecycleId { get; private set; }

    /// <summary>
    /// The project lifecycle assigned to this project.
    /// </summary>
    public ProjectLifecycle? ProjectLifecycle { get; private set; }

    /// <summary>
    /// The project's stack-rank within its portfolio's prioritization view. Null until ranked.
    /// A human override, independent of the computed score.
    /// </summary>
    public int? Rank { get; private set; }

    /// <summary>
    /// The strategic theme tags associated with this project.
    /// </summary>
    public IReadOnlyCollection<StrategicThemeTag<Project>> StrategicThemeTags => _strategicThemeTags;

    /// <summary>
    /// The strategic initiatives associated with this project.
    /// </summary>
    public IReadOnlyCollection<StrategicInitiativeProject> StrategicInitiativeProjects => _strategicInitiativeProjects;

    /// <summary>
    /// The phases associated with this project, created from the assigned lifecycle.
    /// </summary>
    public IReadOnlyCollection<ProjectPhase> Phases => _phases.AsReadOnly();

    /// <summary>
    /// The tasks associated with this project.
    /// </summary>
    public IReadOnlyCollection<ProjectTask> Tasks => _tasks.AsReadOnly();

    /// <summary>
    /// Full history of health checks for this project, ordered by EF as loaded.
    /// Domain invariant: at most one check is non-expired at any instant.
    /// </summary>
    public IReadOnlyCollection<ProjectHealthCheck> HealthChecks => _healthChecks.AsReadOnly();

    /// <summary>
    /// Full history of recorded scores for this project. Each is an immutable snapshot; the current
    /// score is the one with the highest <see cref="ProjectScore.Sequence"/>.
    /// </summary>
    public IReadOnlyCollection<ProjectScore> Scores => _scores.AsReadOnly();

    /// <summary>
    /// A denormalised summary of the project's most recent score (value, date, scorer, model),
    /// maintained for cheap list/grid reads without joining to the score history. Null until the
    /// project has been scored. Always reflects <see cref="LatestScore"/>.
    /// </summary>
    public ScoreSummary? CurrentScore { get; private set; }

    /// <summary>
    /// The project's most recent score snapshot from the loaded history, or null if it has never been
    /// scored. Requires the <see cref="Scores"/> collection to be loaded; for list views prefer the
    /// denormalised <see cref="CurrentScore"/> summary.
    /// </summary>
    public ProjectScore? LatestScore => _scores.OrderByDescending(s => s.Sequence).FirstOrDefault();

    /// <summary>
    /// Indicates whether the project can be deleted.
    /// </summary>
    /// <returns></returns>
    public bool CanBeDeleted() => Status is ProjectStatus.Proposed;

    /// <summary>
    /// Updates the core details of the project.
    /// </summary>
    /// <param name="name">The new name to assign to the project. Cannot be null.</param>
    /// <param name="description">The new description to assign to the project. Cannot be null.</param>
    /// <param name="expenditureCategoryId">The new expenditure category ID to assign to the project.</param>
    /// <param name="timestamp">The timestamp indicating when the update occurred.</param>
    /// <returns></returns>
    public Result UpdateDetails(string name, string description, string? businessCase, string? expectedBenefits, int expenditureCategoryId, Instant timestamp)
    {
        Name = name;
        Description = description;
        BusinessCase = businessCase?.Trim();
        ExpectedBenefits = expectedBenefits?.Trim();
        ExpenditureCategoryId = expenditureCategoryId;

        AddDomainEvent(new ProjectDetailsUpdatedEvent(this, ExpenditureCategoryId, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Assigns an employee to a specific role within the project, allowing multiple employees per role.
    /// </summary>
    public Result AssignRole(ProjectRole role, Guid employeeId)
    {
        return RoleManager.AssignRole(_roles, Id, role, employeeId);
    }

    /// <summary>
    /// Removes an employee from a specific role.
    /// </summary>
    public Result RemoveRole(ProjectRole role, Guid employeeId)
    {
        return RoleManager.RemoveAssignment(_roles, role, employeeId);
    }

    /// <summary>
    /// Updates the roles for the project.
    /// </summary>
    /// <param name="updatedRoles"></param>
    /// <returns></returns>
    public Result UpdateRoles(Dictionary<ProjectRole, HashSet<Guid>> updatedRoles)
    {
        return RoleManager.UpdateRoles(_roles, Id, updatedRoles);
    }

    public Result UpdateTimeline(LocalDateRange? dateRange)
    {
        if (Status is ProjectStatus.Active or ProjectStatus.Completed && dateRange is null)
        {
            return Result.Failure("Active and completed projects must have a start and end date.");
        }

        DateRange = dateRange;

        return Result.Success();
    }

    /// <summary>
    /// Updates the project's key and cascades the change to all linked tasks by updating their task keys.
    /// </summary>
    public Result ChangeKey(ProjectKey key, Instant timestamp)
    {
        Guard.Against.Null(key, nameof(key));

        if (Key.Value == key.Value)
        {
            return Result.Success();
        }

        Key = key;

        AddDomainEvent(new ProjectDetailsUpdatedEvent(this, ExpenditureCategoryId, timestamp));

        foreach (var task in _tasks)
        {
            var updateResult = task.UpdateProjectKey(Key);
            if (updateResult.IsFailure)
            {
                return updateResult;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Updates the project's program association.
    /// </summary>
    /// <param name="program"></param>
    /// <returns></returns>
    internal Result UpdateProgram(Program? program)
    {
        if (program is null)
        {
            ProgramId = null;
            return Result.Success();
        }

        if (program.PortfolioId != PortfolioId)
        {
            return Result.Failure("The project must belong to the same portfolio as the program.");
        }

        ProgramId = program.Id;

        return Result.Success();
    }

    /// <summary>
    /// Associates a strategic theme with this project.
    /// </summary>
    public Result AddStrategicTheme(Guid strategicThemeId)
    {
        Guard.Against.NullOrEmpty(strategicThemeId, nameof(strategicThemeId));

        return StrategicThemeTagManager<Project>.AddStrategicThemeTag(_strategicThemeTags, Id, strategicThemeId, "project");
    }

    /// <summary>
    /// Removes a strategic theme from this project.
    /// </summary>
    public Result RemoveStrategicTheme(Guid strategicThemeId)
    {
        Guard.Against.NullOrEmpty(strategicThemeId, nameof(strategicThemeId));

        return StrategicThemeTagManager<Project>.RemoveStrategicThemeTag(_strategicThemeTags, strategicThemeId, "project");
    }

    /// <summary>
    /// Updates the strategic themes associated with this project.
    /// </summary>
    /// <param name="strategicThemeIds"></param>
    /// <returns></returns>
    public Result UpdateStrategicThemes(HashSet<Guid> strategicThemeIds)
    {
        Guard.Against.Null(strategicThemeIds, nameof(strategicThemeIds));

        return StrategicThemeTagManager<Project>.UpdateTags(_strategicThemeTags, Id, strategicThemeIds, "project");
    }

    #region Lifecycle

    /// <summary>
    /// Assigns a project lifecycle to this project, creating phase instances from the lifecycle template.
    /// Only allowed when the project is in Proposed or Approved state and no lifecycle is currently assigned.
    /// </summary>
    /// <param name="lifecycle">The lifecycle to assign. Must be in Active state.</param>
    public Result AssignLifecycle(ProjectLifecycle lifecycle)
    {
        Guard.Against.Null(lifecycle, nameof(lifecycle));

        if (IsClosed)
        {
            return Result.Failure("Cannot assign a lifecycle to a closed project.");
        }

        if (lifecycle.State != ProjectLifecycleState.Active)
        {
            return Result.Failure("Only active lifecycles can be assigned to projects.");
        }

        if (ProjectLifecycleId.HasValue)
        {
            return Result.Failure("A lifecycle is already assigned to this project. Use ChangeLifecycle to switch lifecycles.");
        }

        ProjectLifecycleId = lifecycle.Id;

        foreach (var lifecyclePhase in lifecycle.Phases.OrderBy(p => p.Order))
        {
            _phases.Add(ProjectPhase.Create(Id, lifecyclePhase));
        }

        return Result.Success();
    }

    /// <summary>
    /// Changes the project's lifecycle to a new one, remapping tasks from old phases to new phases.
    /// </summary>
    /// <param name="newLifecycle">The new lifecycle to assign.</param>
    /// <param name="phaseMapping">Maps old phase IDs to new lifecycle phase IDs (template phase IDs from the new lifecycle).</param>
    public Result ChangeLifecycle(ProjectLifecycle newLifecycle, Dictionary<Guid, Guid> phaseMapping)
    {
        Guard.Against.Null(newLifecycle, nameof(newLifecycle));
        Guard.Against.Null(phaseMapping, nameof(phaseMapping));

        if (IsClosed)
        {
            return Result.Failure("Cannot change the lifecycle of a closed project.");
        }

        if (!ProjectLifecycleId.HasValue)
        {
            return Result.Failure("No lifecycle is currently assigned. Use AssignLifecycle instead.");
        }

        if (newLifecycle.State != ProjectLifecycleState.Active)
        {
            return Result.Failure("Only active lifecycles can be assigned to projects.");
        }

        if (newLifecycle.Id == ProjectLifecycleId.Value)
        {
            return Result.Failure("The new lifecycle must be different from the current lifecycle.");
        }

        // Validate that every phase with tasks is included in the mapping
        var phasesWithTasks = _phases
            .Where(p => _tasks.Any(t => t.ProjectPhaseId == p.Id))
            .ToList();

        foreach (var phase in phasesWithTasks)
        {
            if (!phaseMapping.ContainsKey(phase.Id))
            {
                return Result.Failure($"Phase '{phase.Name}' has tasks but is not included in the phase mapping.");
            }
        }

        // Validate that every mapping target is a valid phase in the new lifecycle
        var newLifecyclePhaseIds = newLifecycle.Phases.Select(p => p.Id).ToHashSet();
        foreach (var (oldPhaseId, newLifecyclePhaseId) in phaseMapping)
        {
            if (!newLifecyclePhaseIds.Contains(newLifecyclePhaseId))
            {
                return Result.Failure($"Target lifecycle phase '{newLifecyclePhaseId}' does not exist in the new lifecycle.");
            }
        }

        // Create new project phases from the new lifecycle
        var newPhases = new List<ProjectPhase>();
        foreach (var lifecyclePhase in newLifecycle.Phases.OrderBy(p => p.Order))
        {
            newPhases.Add(ProjectPhase.Create(Id, lifecyclePhase));
        }

        // Build lookup: old phase ID → new project phase ID (via lifecycle phase ID mapping)
        // phaseMapping: oldProjectPhaseId → newLifecyclePhaseId
        // newPhases: each has ProjectLifecyclePhaseId matching the lifecycle phase
        var oldToNewPhaseMap = new Dictionary<Guid, Guid>();
        foreach (var (oldPhaseId, newLifecyclePhaseId) in phaseMapping)
        {
            var newPhase = newPhases.FirstOrDefault(p => p.ProjectLifecyclePhaseId == newLifecyclePhaseId);
            if (newPhase is null)
            {
                return Result.Failure($"Could not find new phase for lifecycle phase '{newLifecyclePhaseId}'.");
            }
            oldToNewPhaseMap[oldPhaseId] = newPhase.Id;
        }

        // Remap all tasks to new phases
        foreach (var task in _tasks)
        {
            if (oldToNewPhaseMap.TryGetValue(task.ProjectPhaseId, out var newPhaseId))
            {
                task.ChangePhase(newPhaseId);
            }
            else
            {
                // Phase had no tasks (wasn't in the mapping), assign to first new phase
                task.ChangePhase(newPhases.First().Id);
            }
        }

        // Remove old phases and add new ones
        _phases.Clear();
        foreach (var newPhase in newPhases)
        {
            _phases.Add(newPhase);
        }

        ProjectLifecycleId = newLifecycle.Id;

        return Result.Success();
    }

    /// <summary>
    /// Approves the project. A lifecycle must be assigned before approval.
    /// </summary>
    public Result Approve()
    {
        if (Status != ProjectStatus.Proposed)
        {
            return Result.Failure("Only proposed projects can be approved.");
        }

        if (!ProjectLifecycleId.HasValue)
        {
            return Result.Failure("A project lifecycle must be assigned before the project can be approved.");
        }

        Status = ProjectStatus.Approved;

        return Result.Success();
    }

    /// <summary>
    /// Activates the project.
    /// </summary>
    public Result Activate()
    {
        if (Status is not (ProjectStatus.Proposed or ProjectStatus.Approved))
        {
            return Result.Failure("Only proposed or approved projects can be activated.");
        }

        if (DateRange is null)
        {
            return Result.Failure("The project must have a start and end date before it can be activated.");
        }

        Status = ProjectStatus.Active;

        return Result.Success();
    }

    /// <summary>
    /// Marks the project as completed.
    /// </summary>
    public Result Complete()
    {
        if (Status != ProjectStatus.Active)
        {
            return Result.Failure("Only active projects can be completed.");
        }

        if (DateRange == null)
        {
            return Result.Failure("The project must have a start and end date before it can be completed.");
        }

        Status = ProjectStatus.Completed;

        return Result.Success();
    }

    /// <summary>
    /// Cancels the project.
    /// </summary>
    public Result Cancel()
    {
        if (Status is ProjectStatus.Completed or ProjectStatus.Cancelled)
        {
            return Result.Failure("The project is already completed or cancelled.");
        }

        Status = ProjectStatus.Cancelled;

        return Result.Success();
    }

    #endregion Lifecycle

    /// <summary>
    /// Checks if the portfolio is active on the specified date.
    /// </summary>
    public bool IsActiveOn(LocalDate date)
    {
        Guard.Against.Null(date, nameof(date));

        return DateRange is not null && DateRange.IsActiveOn(date);
    }

    #region Tasks

    /// <summary>
    /// Creates a new project task with the specified details and adds it to the project.
    /// A lifecycle must be assigned before tasks can be created. The parentId can reference either
    /// a project phase (for root-level tasks) or an existing task (for child tasks).
    /// </summary>
    /// <param name="nextNumber">The next available task number to assign to the new task.</param>
    /// <param name="name">The name of the task. Cannot be null or empty.</param>
    /// <param name="description">An optional description providing additional details about the task.</param>
    /// <param name="type">The type of the task to create (Task or Milestone).</param>
    /// <param name="status">The initial status to assign to the new task.</param>
    /// <param name="priority">The priority level of the task.</param>
    /// <param name="progress">The current progress of the task, or null if not specified.</param>
    /// <param name="parentId">The ID of the parent phase or task. If it matches a phase, the task becomes a root task
    /// in that phase. If it matches a task, the new task becomes a child of that task and inherits the phase.</param>
    /// <param name="plannedDateRange">The planned date range for the task, or null if not specified.</param>
    /// <param name="plannedDate">The planned date for the task, or null if not specified.</param>
    /// <param name="estimatedEffortHours">The estimated effort required to complete the task, in hours.</param>
    /// <param name="roles">A mapping of task roles to sets of employee identifiers assigned to each role.</param>
    /// <returns>A result containing the newly created project task if successful; otherwise, a failure result.</returns>
    public Result<ProjectTask> CreateTask(
        int nextNumber,
        string name,
        string? description,
        ProjectTaskType type,
        Enums.TaskStatus status,
        TaskPriority priority,
        Progress? progress,
        Guid parentId,
        FlexibleDateRange? plannedDateRange,
        LocalDate? plannedDate,
        decimal? estimatedEffortHours,
        Dictionary<TaskRole, HashSet<Guid>>? roles)
    {
        if (Key is null)
        {
            return Result.Failure<ProjectTask>("Project must have a key before tasks can be created.");
        }

        if (!ProjectLifecycleId.HasValue)
        {
            return Result.Failure<ProjectTask>("A lifecycle must be assigned before tasks can be created.");
        }

        // Resolve parentId — could be a phase or a task
        var resolveResult = ResolveParent(parentId);
        if (resolveResult.IsFailure)
        {
            return Result.Failure<ProjectTask>(resolveResult.Error);
        }

        var (phase, parentTask) = resolveResult.Value;
        var projectPhaseId = phase.Id;
        Guid? taskParentId = parentTask?.Id;

        // Calculate order — root tasks scoped to phase, child tasks scoped to parent
        var siblings = taskParentId.HasValue
            ? _tasks.Where(t => t.ParentId == taskParentId)
            : _tasks.Where(t => t.ParentId is null && t.ProjectPhaseId == projectPhaseId);
        var order = siblings.Any() ? siblings.Max(t => t.Order) + 1 : 1;

        var task = ProjectTask.Create(
            Id,
            new ProjectTaskKey(Key, nextNumber),
            name,
            description,
            type,
            status,
            priority,
            progress,
            order,
            taskParentId,
            projectPhaseId,
            plannedDateRange,
            plannedDate,
            estimatedEffortHours,
            roles);

        _tasks.Add(task);

        parentTask?.AddChild(task);

        return Result.Success(task);
    }

    /// <summary>
    /// Changes the placement of a task within the project hierarchy. The newParentId can reference
    /// either a project phase (moving the task to root level in that phase) or another task (making it a child).
    /// When a task moves across phases, all its descendants are updated to the new phase.
    /// </summary>
    /// <param name="taskId">The ID of the task to move.</param>
    /// <param name="newParentId">The ID of the target phase or parent task.</param>
    /// <param name="order">The desired position within the new parent's children, or null to append at the end.</param>
    public Result ChangeTaskPlacement(Guid taskId, Guid newParentId, int? order)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null)
        {
            return Result.Failure("Task not found.");
        }

        if (order.HasValue && order.Value < 1)
        {
            return Result.Failure("Order must be greater than zero.");
        }

        // Resolve newParentId — could be a phase or a task
        var resolveResult = ResolveParent(newParentId);
        if (resolveResult.IsFailure)
        {
            return Result.Failure(resolveResult.Error);
        }

        var (targetPhase, newParentTask) = resolveResult.Value;
        Guid? newTaskParentId = newParentTask?.Id;
        var newPhaseId = targetPhase.Id;

        // Validate: milestone cannot have children
        if (newParentTask?.Type == ProjectTaskType.Milestone)
        {
            return Result.Failure("Milestones cannot have child tasks.");
        }

        // Validate: prevent circular references
        if (newTaskParentId.HasValue && newTaskParentId == taskId)
        {
            return Result.Failure("A task cannot be its own parent.");
        }
        if (newTaskParentId.HasValue && task.IsDescendantOf(newTaskParentId.Value))
        {
            return Result.Failure("A task cannot be moved under one of its descendants.");
        }

        var origParentId = task.ParentId;
        var origPhaseId = task.ProjectPhaseId;
        var isChangingParent = origParentId != newTaskParentId;
        var isChangingPhase = origPhaseId != newPhaseId;

        // Determine siblings in the target location
        var siblingsQuery = newTaskParentId.HasValue
            ? _tasks.Where(t => t.ParentId == newTaskParentId)
            : _tasks.Where(t => t.ParentId is null && t.ProjectPhaseId == newPhaseId);

        if (!isChangingParent)
        {
            siblingsQuery = siblingsQuery.Where(t => t.Id != taskId);
        }

        var childrenCount = siblingsQuery.Count() + 1;
        var newOrder = Math.Min(order ?? childrenCount, childrenCount);

        Result changeResult;
        if (isChangingParent)
        {
            // Remove from old parent
            if (origParentId.HasValue)
            {
                var oldParent = _tasks.FirstOrDefault(t => t.Id == origParentId);
                oldParent?.RemoveChild(task);
            }

            // Shift siblings in target to make room
            foreach (var sibling in siblingsQuery.Where(t => t.Order >= newOrder))
            {
                sibling.ChangeOrder(sibling.Order + 1);
            }

            changeResult = task.ChangeParent(newTaskParentId, newOrder);

            newParentTask?.AddChild(task);

            // Update phase if changed
            if (isChangingPhase)
            {
                task.ChangePhase(newPhaseId);
                foreach (var descendant in GetDescendants(task))
                {
                    descendant.ChangePhase(newPhaseId);
                }
            }

            // Reorder old parent's remaining children
            if (origParentId.HasValue)
            {
                ResetOrderForChildTasks(origParentId);
            }
            else
            {
                ResetOrderForRootTasksInPhase(origPhaseId);
            }
        }
        else if (isChangingPhase)
        {
            // Same parent (null) but different phase — moving root task between phases
            // Shift siblings in target phase
            foreach (var sibling in siblingsQuery.Where(t => t.Order >= newOrder))
            {
                sibling.ChangeOrder(sibling.Order + 1);
            }

            task.ChangePhase(newPhaseId);
            task.ChangeOrder(newOrder);
            foreach (var descendant in GetDescendants(task))
            {
                descendant.ChangePhase(newPhaseId);
            }

            // Reorder old phase
            ResetOrderForRootTasksInPhase(origPhaseId);
        }
        else
        {
            // Same parent, same phase — just reordering
            var previousOrder = task.Order;

            if (newOrder == previousOrder)
                return Result.Success();

            if (newOrder < previousOrder)
            {
                foreach (var sibling in siblingsQuery.Where(t => t.Order >= newOrder && t.Order < previousOrder))
                {
                    sibling.ChangeOrder(sibling.Order + 1);
                }
            }
            else
            {
                foreach (var sibling in siblingsQuery.Where(t => t.Order > previousOrder && t.Order <= newOrder))
                {
                    sibling.ChangeOrder(sibling.Order - 1);
                }
            }

            changeResult = task.ChangeOrder(newOrder);
            return changeResult;
        }

        return Result.Success();
    }

    /// <summary>
    /// Deletes the task with the specified identifier if it has no child tasks or active dependencies.
    /// </summary>
    /// <remarks>A task cannot be deleted if it has child tasks or if it has active dependencies. Remove all
    /// child tasks and dependencies before attempting to delete the task.</remarks>
    /// <param name="taskId">The unique identifier of the task to delete.</param>
    /// <returns>A Result indicating whether the deletion was successful. Returns a failure result if the task does not exist,
    /// has child tasks, or has active dependencies.</returns>
    public Result DeleteTask(Guid taskId)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null)
        {
            return Result.Failure("Task not found.");
        }

        // Check if task has children
        if (task.Children.Count > 0)
        {
            return Result.Failure("Cannot delete a task with children. Delete child tasks first.");
        }

        // Check if task has active dependencies
        if (task.Successors.Any(d => d.IsActive) || task.Predecessors.Any(d => d.IsActive))
        {
            return Result.Failure("Cannot delete a task with active dependencies. Remove dependencies first.");
        }

        var parentId = task.ParentId;

        _tasks.Remove(task);

        ResetOrderForChildTasks(parentId);

        return Result.Success();
    }

    /// <summary>
    /// Resolves a parentId to either a phase (root task) or a parent task (child task).
    /// Returns the resolved phase and optional parent task.
    /// </summary>
    private Result<(ProjectPhase Phase, ProjectTask? ParentTask)> ResolveParent(Guid parentId)
    {
        // Check if parentId is a phase
        var phase = _phases.FirstOrDefault(p => p.Id == parentId);
        if (phase is not null)
        {
            return Result.Success<(ProjectPhase, ProjectTask?)>((phase, null));
        }

        // Check if parentId is a task
        var parentTask = _tasks.FirstOrDefault(t => t.Id == parentId);
        if (parentTask is null)
        {
            return Result.Failure<(ProjectPhase, ProjectTask?)>("The specified parent was not found. It must be a valid phase or task within this project.");
        }

        // Resolve the phase from the parent task
        var taskPhase = _phases.FirstOrDefault(p => p.Id == parentTask.ProjectPhaseId);
        if (taskPhase is null)
        {
            return Result.Failure<(ProjectPhase, ProjectTask?)>("Unable to resolve the phase for the parent task.");
        }

        return Result.Success<(ProjectPhase, ProjectTask?)>((taskPhase, parentTask));
    }

    private List<ProjectTask> GetDescendants(ProjectTask task)
    {
        var descendants = new List<ProjectTask>();
        var queue = new Queue<Guid>();
        queue.Enqueue(task.Id);

        while (queue.Count > 0)
        {
            var parentId = queue.Dequeue();
            foreach (var child in _tasks.Where(t => t.ParentId == parentId))
            {
                descendants.Add(child);
                queue.Enqueue(child.Id);
            }
        }

        return descendants;
    }

    private void ResetOrderForRootTasksInPhase(Guid phaseId)
    {
        var siblings = _tasks.Where(t => t.ParentId is null && t.ProjectPhaseId == phaseId).OrderBy(t => t.Order);
        int order = 1;
        foreach (var sibling in siblings)
        {
            sibling.ChangeOrder(order);
            order++;
        }
    }

    // TODO: this really belongs on the ProjectTask so a parent can reset its own children, but root tasks have no parent
    private void ResetOrderForChildTasks(Guid? parentId)
    {
        var siblings = parentId.HasValue
            ? _tasks.Where(t => t.ParentId == parentId.Value)
            : _tasks.Where(t => t.ParentId is null);
        if (siblings.Any())
        {
            int order = 1;
            foreach (var sibling in siblings.OrderBy(t => t.Order))
            {
                sibling.ChangeOrder(order);
                order++;
            }
        }
    }

    #endregion Tasks

    #region Scoring

    private const string UnauthorizedScoreActorError =
        "Only the project's owner or manager — or the parent portfolio's or program's owner or manager — may score this project.";

    /// <summary>
    /// Records a new score for the project from a calculated scoring model result, capturing an immutable
    /// snapshot of the ratings and computed outputs. The actor must be authorized per <see cref="CanManageProject"/>.
    /// The supplied <paramref name="model"/> is read only — it is never attached to this aggregate.
    /// </summary>
    /// <param name="model">The active scoring model assigned to the project's portfolio.</param>
    /// <param name="ratingValuesByCriterionId">The numeric rating value for each criterion, keyed by criterion ID.</param>
    /// <param name="selectedLevels">For scale-rated criteria, the selected (level id, label) keyed by criterion ID.</param>
    /// <param name="actorEmployeeId">The ID of the employee recording the score.</param>
    /// <param name="portfolioRoles">The actor's roles in the parent portfolio.</param>
    /// <param name="programRoles">The actor's roles in the parent program, if applicable.</param>
    /// <param name="timestamp">The current time.</param>
    public Result<ProjectScore> RecordScore(
        ScoringModel model,
        IReadOnlyDictionary<Guid, decimal> ratingValuesByCriterionId,
        IReadOnlyDictionary<Guid, (Guid LevelId, string Label)>? selectedLevels,
        Guid actorEmployeeId,
        IEnumerable<RoleAssignment<ProjectPortfolioRole>> portfolioRoles,
        IEnumerable<RoleAssignment<ProgramRole>>? programRoles,
        Instant timestamp)
    {
        Guard.Against.Null(model, nameof(model));
        Guard.Against.Null(ratingValuesByCriterionId, nameof(ratingValuesByCriterionId));

        if (!CanManageProject(actorEmployeeId, portfolioRoles, programRoles))
        {
            return Result.Failure<ProjectScore>(UnauthorizedScoreActorError);
        }

        var calculation = model.CalculateScore(ratingValuesByCriterionId);
        if (calculation.IsFailure)
        {
            return Result.Failure<ProjectScore>(calculation.Error);
        }

        var sequence = _scores.Count == 0 ? 1 : _scores.Max(s => s.Sequence) + 1;

        var score = ProjectScore.CreateSnapshot(
            Id,
            model,
            calculation.Value,
            ratingValuesByCriterionId,
            selectedLevels,
            actorEmployeeId,
            timestamp,
            sequence);

        _scores.Add(score);

        CurrentScore = new ScoreSummary(
            calculation.Value.PrimaryValue,
            timestamp,
            actorEmployeeId,
            model.Name);

        return Result.Success(score);
    }

    #endregion Scoring

    #region Health Checks

    private const string UnauthorizedHealthCheckActorError =
        "Only the project's owner or manager — or the parent portfolio's or program's owner or manager — may manage health checks.";

    /// <summary>
    /// Adds a new health check to the project, attributed to <paramref name="actorEmployeeId"/>.
    /// The actor must be authorized per <see cref="CanManageProject"/>; if the latest
    /// existing check has not yet expired at <paramref name="now"/>, its expiration is truncated
    /// so that no two checks are active simultaneously.
    /// </summary>
    /// <param name="status">The health status of the new check.</param>
    /// <param name="actorEmployeeId">The ID of the employee adding the health check.</param>
    /// <param name="portfolioRoles">The roles of the actor in the portfolio.</param>
    /// <param name="programRoles">The roles of the actor in the program, if applicable.</param>
    /// <param name="expiration">The expiration time of the health check.</param>
    /// <param name="note">An optional note for the health check.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The result of the operation, including the newly added health check if successful.</returns>
    public Result<ProjectHealthCheck> AddHealthCheck(
        HealthStatus status,
        Guid actorEmployeeId,
        IEnumerable<RoleAssignment<ProjectPortfolioRole>> portfolioRoles,
        IEnumerable<RoleAssignment<ProgramRole>>? programRoles,
        Instant expiration,
        string? note,
        Instant now)
    {
        if (!CanManageProject(actorEmployeeId, portfolioRoles, programRoles))
            return Result.Failure<ProjectHealthCheck>(UnauthorizedHealthCheckActorError);

        if (expiration <= now)
            return Result.Failure<ProjectHealthCheck>("Expiration must be in the future.");

        var newCheck = new ProjectHealthCheck(Id, status, actorEmployeeId, now, expiration, note);

        var report = new HealthReport<ProjectHealthCheck>(_healthChecks);
        report.Add(newCheck, now);

        _healthChecks.Add(newCheck);

        return Result.Success(newCheck);
    }

    /// <summary>
    /// Updates an existing (non-expired) health check on this project. The actor must be
    /// authorized at the time of the action — original reporters do not get special treatment.
    /// </summary>
    /// <param name="healthCheckId">The ID of the health check to update.</param>
    /// <param name="actorEmployeeId">The ID of the employee updating the health check.</param>
    /// <param name="portfolioRoles">The roles of the actor in the portfolio.</param>
    /// <param name="programRoles">The roles of the actor in the program, if applicable.</param>
    /// <param name="status">The new health status.</param>
    /// <param name="expiration">The new expiration time.</param>
    /// <param name="note">An optional note for the health check.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The result of the operation, including the updated health check if successful.</returns>
    public Result<ProjectHealthCheck> UpdateHealthCheck(
        Guid healthCheckId,
        Guid actorEmployeeId,
        IEnumerable<RoleAssignment<ProjectPortfolioRole>> portfolioRoles,
        IEnumerable<RoleAssignment<ProgramRole>>? programRoles,
        HealthStatus status,
        Instant expiration,
        string? note,
        Instant now)
    {
        if (!CanManageProject(actorEmployeeId, portfolioRoles, programRoles))
            return Result.Failure<ProjectHealthCheck>(UnauthorizedHealthCheckActorError);

        var healthCheck = _healthChecks.FirstOrDefault(h => h.Id == healthCheckId);
        if (healthCheck is null)
            return Result.Failure<ProjectHealthCheck>($"Health check {healthCheckId} not found on project {Id}.");

        var updateResult = healthCheck.Update(status, expiration, note, now);
        if (updateResult.IsFailure)
            return Result.Failure<ProjectHealthCheck>(updateResult.Error);

        return Result.Success(healthCheck);
    }

    /// <summary>
    /// Removes a health check from this project. The actor must be authorized at the time of the action.
    /// </summary>
    /// <param name="healthCheckId">The ID of the health check to remove.</param>
    /// <param name="actorEmployeeId">The ID of the employee removing the health check.</param>
    /// <param name="portfolioRoles">The roles of the actor in the portfolio.</param>
    /// <param name="programRoles">The roles of the actor in the program, if applicable.</param>
    /// <returns>The result of the operation, including the removed health check if successful.</returns>
    public Result<ProjectHealthCheck> RemoveHealthCheck(
        Guid healthCheckId,
        Guid actorEmployeeId,
        IEnumerable<RoleAssignment<ProjectPortfolioRole>> portfolioRoles,
        IEnumerable<RoleAssignment<ProgramRole>>? programRoles)
    {
        if (!CanManageProject(actorEmployeeId, portfolioRoles, programRoles))
            return Result.Failure<ProjectHealthCheck>(UnauthorizedHealthCheckActorError);

        var healthCheck = _healthChecks.FirstOrDefault(h => h.Id == healthCheckId);
        if (healthCheck is null)
            return Result.Failure<ProjectHealthCheck>($"Health check {healthCheckId} not found on project {Id}.");

        _healthChecks.Remove(healthCheck);
        return Result.Success(healthCheck);
    }

    /// <summary>
    /// Read-side authorization predicate: returns true if the given employee may manage this project —
    /// the delivery-management capability shared by actions such as recording health checks and scores.
    /// Owner/Manager on the project itself OR on the parent portfolio OR on the parent program (when
    /// assigned) is sufficient. Sponsors are intentionally excluded — they fund and oversee but don't
    /// run delivery.
    ///
    /// The aggregate's management methods enforce the same rule inline, so callers cannot bypass it;
    /// this method exists primarily so the API layer can surface the decision to the UI for
    /// action-availability hints.
    /// </summary>
    /// <param name="employeeId">The ID of the employee to check.</param>
    /// <param name="portfolioRoles">The roles of the employee in the portfolio.</param>
    /// <param name="programRoles">The roles of the employee in the program, if applicable.</param>
    /// <returns>True if the employee can manage the project; otherwise, false.</returns>
    public bool CanManageProject(
        Guid employeeId,
        IEnumerable<RoleAssignment<ProjectPortfolioRole>> portfolioRoles,
        IEnumerable<RoleAssignment<ProgramRole>>? programRoles)
    {
        if (_roles.Any(r => r.EmployeeId == employeeId && r.Role is ProjectRole.Owner or ProjectRole.Manager))
            return true;

        if (portfolioRoles.Any(r => r.EmployeeId == employeeId && r.Role is ProjectPortfolioRole.Owner or ProjectPortfolioRole.Manager))
            return true;

        if (programRoles is not null && programRoles.Any(r => r.EmployeeId == employeeId && r.Role is ProgramRole.Owner or ProgramRole.Manager))
            return true;

        return false;
    }

    #endregion Health Checks

    /// <summary>
    /// Creates a new project.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="description"></param>
    /// <param name="key"></param>
    /// <param name="expenditureCategoryId"></param>
    /// <param name="dateRange"></param>
    /// <param name="portfolioId"></param>
    /// <param name="programId"></param>
    /// <param name="roles"></param>
    /// <param name="strategicThemes"></param>
    /// <param name="timestamp"></param>
    /// <returns></returns>
    internal static Project Create(string name, string description, ProjectKey key, int expenditureCategoryId, LocalDateRange? dateRange, Guid portfolioId, Guid? programId, string? businessCase, string? expectedBenefits, Dictionary<ProjectRole, HashSet<Guid>>? roles, HashSet<Guid>? strategicThemes, Instant timestamp)
    {
        var project = new Project(name, description, key, ProjectStatus.Proposed, expenditureCategoryId, dateRange, portfolioId, programId, businessCase, expectedBenefits, roles, strategicThemes);

        project.AddPostPersistenceAction(() => project.AddDomainEvent(
            new ProjectCreatedEvent
            (
                project,
                project.ExpenditureCategoryId,
                (int)project.Status,
                project.DateRange,
                project.PortfolioId,
                project.ProgramId,
                project.Roles
                    .GroupBy(x => (int)x.Role)
                    .ToDictionary(x => x.Key, x => x.Select(y => y.EmployeeId).ToArray()),
                [.. project.StrategicThemeTags.Select(x => x.StrategicThemeId)],
                timestamp
            )));

        return project;
    }
}
