using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

/// <summary>
/// Represents a phase instance on a project, created from a project lifecycle phase template.
/// Phases provide the top-level structure for a project's plan and group related tasks.
/// </summary>
public sealed class ProjectPhase : BaseAuditableEntity
{
    private readonly HashSet<RoleAssignment<ProjectPhaseRole>> _roles = [];

    private ProjectPhase() { }

    private ProjectPhase(Guid projectId, ProjectLifecyclePhase lifecyclePhase)
    {
        ProjectId = projectId;
        ProjectLifecyclePhaseId = lifecyclePhase.Id;
        Name = lifecyclePhase.Name;
        Description = lifecyclePhase.Description;
        Status = TaskStatus.NotStarted;
        Order = lifecyclePhase.Order;
        Progress = Progress.NotStarted();
    }

    /// <summary>
    /// The ID of the project this phase belongs to.
    /// </summary>
    public Guid ProjectId { get; private init; }

    /// <summary>
    /// The ID of the lifecycle phase template this phase was created from.
    /// </summary>
    public Guid ProjectLifecyclePhaseId { get; private init; }

    /// <summary>
    /// The name of the phase. Copied from the lifecycle template and not editable.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// A description of the phase's purpose. Defaults from the lifecycle template but is editable per project.
    /// </summary>
    public string Description
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Description)).Trim();
    } = default!;

    /// <summary>
    /// The current status of the phase.
    /// </summary>
    public TaskStatus Status { get; private set; }

    /// <summary>
    /// The display order of the phase within the project. From the lifecycle template, not editable.
    /// </summary>
    public int Order { get; private set; }

    /// <summary>
    /// The planned date range for the phase.
    /// </summary>
    public FlexibleDateRange? DateRange { get; private set; }

    /// <summary>
    /// The current progress of the phase as a percentage (0-100).
    /// </summary>
    public Progress Progress { get; private set; } = null!;

    /// <summary>
    /// The role assignments for this phase (e.g., assignees, reviewers).
    /// </summary>
    public IReadOnlyCollection<RoleAssignment<ProjectPhaseRole>> Roles => _roles;

    /// <summary>
    /// Updates the description of the phase.
    /// </summary>
    public Result UpdateDescription(string description)
    {
        Description = description;
        return Result.Success();
    }

    /// <summary>
    /// Updates the status of the phase.
    /// </summary>
    public Result UpdateStatus(TaskStatus status)
    {
        Status = status;
        return Result.Success();
    }

    /// <summary>
    /// Updates the planned date range for the phase.
    /// </summary>
    public Result UpdatePlannedDates(FlexibleDateRange? dateRange)
    {
        DateRange = dateRange;
        return Result.Success();
    }

    /// <summary>
    /// Updates the planned date range for the phase, validating that it contains all dated root tasks.
    /// </summary>
    internal Result UpdatePlannedDates(FlexibleDateRange? dateRange, IEnumerable<ProjectTask> rootTasks)
    {
        var rootTaskList = rootTasks.ToList();

        if (dateRange is null)
        {
            if (rootTaskList.Any(t => t.Type == ProjectTaskType.Milestone ? t.PlannedDate.HasValue : t.PlannedDateRange is not null))
            {
                return Result.Failure("A phase cannot be updated to null when it has root tasks with dates.");
            }
            DateRange = null;
            return Result.Success();
        }

        if (rootTaskList.Count > 0 && TryGetShiftDays(dateRange, out var days))
        {
            foreach (var task in rootTaskList)
            {
                task.ShiftDates(days);
            }

            DateRange = dateRange;
            return Result.Success();
        }

        foreach (var task in rootTaskList)
        {
            if (task.Type == ProjectTaskType.Milestone)
            {
                if (task.PlannedDate.HasValue)
                {
                    var date = task.PlannedDate.Value;
                    if (date < dateRange.Start || (dateRange.End.HasValue && date > dateRange.End.Value))
                    {
                        return Result.Failure(
                            $"The date range must contain all child items. \"{task.Name}\" falls outside the selected range.");
                    }
                }
            }
            else
            {
                if (task.PlannedDateRange is not null)
                {
                    var start = task.PlannedDateRange.Start;
                    var end = task.PlannedDateRange.End;
                    if (start < dateRange.Start || (dateRange.End.HasValue && (!end.HasValue || end.Value > dateRange.End.Value)))
                    {
                        return Result.Failure(
                            $"The date range must contain all child items. \"{task.Name}\" falls outside the selected range.");
                    }
                }
            }
        }

        DateRange = dateRange;
        return Result.Success();
    }

    private bool TryGetShiftDays(FlexibleDateRange newRange, out int days)
    {
        days = 0;
        if (DateRange is null)
        {
            return false;
        }

        if (DateRange.End.HasValue != newRange.End.HasValue)
        {
            return false;
        }

        var startDelta = Period.DaysBetween(DateRange.Start, newRange.Start);

        if (DateRange.End.HasValue && newRange.End.HasValue)
        {
            var endDelta = Period.DaysBetween(DateRange.End.Value, newRange.End.Value);
            days = startDelta;
            return startDelta != 0 && startDelta == endDelta;
        }

        days = startDelta;
        return startDelta != 0;
    }

    /// <summary>
    /// Updates the progress of the phase.
    /// </summary>
    public Result UpdateProgress(Progress progress)
    {
        Guard.Against.Null(progress, nameof(progress));

        Progress = progress;
        return Result.Success();
    }

    /// <summary>
    /// Updates all role assignments for this phase.
    /// </summary>
    public Result UpdateRoles(Dictionary<ProjectPhaseRole, HashSet<Guid>> updatedRoles)
    {
        return RoleManager.UpdateRoles(_roles, Id, updatedRoles);
    }

    /// <summary>
    /// Creates a new project phase from a lifecycle phase template.
    /// </summary>
    internal static ProjectPhase Create(Guid projectId, ProjectLifecyclePhase lifecyclePhase)
    {
        Guard.Against.Null(lifecyclePhase, nameof(lifecyclePhase));

        return new ProjectPhase(projectId, lifecyclePhase);
    }
}
