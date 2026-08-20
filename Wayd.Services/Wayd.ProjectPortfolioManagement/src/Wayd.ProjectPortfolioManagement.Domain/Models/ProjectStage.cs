using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

/// <summary>
/// Represents a stage instance on a project, created from a project lifecycle stage template.
/// Stages provide the top-level structure for a project's plan and group related tasks.
/// </summary>
public sealed class ProjectStage : BaseAuditableEntity
{
    private readonly HashSet<RoleAssignment<ProjectStageRole>> _roles = [];

    private ProjectStage() { }

    private ProjectStage(Guid projectId, ProjectLifecycleStage lifecycleStage)
    {
        ProjectId = projectId;
        ProjectLifecycleStageId = lifecycleStage.Id;
        Name = lifecycleStage.Name;
        Description = lifecycleStage.Description;
        Status = TaskStatus.NotStarted;
        Order = lifecycleStage.Order;
        Progress = Progress.NotStarted();
    }

    /// <summary>
    /// The ID of the project this stage belongs to.
    /// </summary>
    public Guid ProjectId { get; private init; }

    /// <summary>
    /// The ID of the lifecycle stage template this stage was created from.
    /// </summary>
    public Guid ProjectLifecycleStageId { get; private init; }

    /// <summary>
    /// The name of the stage. Copied from the lifecycle template and not editable.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// A description of the stage's purpose. Defaults from the lifecycle template but is editable per project.
    /// </summary>
    public string Description
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Description)).Trim();
    } = default!;

    /// <summary>
    /// The current status of the stage.
    /// </summary>
    public TaskStatus Status { get; private set; }

    /// <summary>
    /// The display order of the stage within the project. From the lifecycle template, not editable.
    /// </summary>
    public int Order { get; private set; }

    /// <summary>
    /// The planned date range for the stage.
    /// </summary>
    public FlexibleDateRange? DateRange { get; private set; }

    /// <summary>
    /// The current progress of the stage as a percentage (0-100).
    /// </summary>
    public Progress Progress { get; private set; } = null!;

    /// <summary>
    /// The role assignments for this stage (e.g., assignees, reviewers).
    /// </summary>
    public IReadOnlyCollection<RoleAssignment<ProjectStageRole>> Roles => _roles;

    /// <summary>
    /// Updates the description of the stage.
    /// </summary>
    public Result UpdateDescription(string description)
    {
        Description = description;
        return Result.Success();
    }

    /// <summary>
    /// Updates the status of the stage.
    /// </summary>
    public Result UpdateStatus(TaskStatus status)
    {
        Status = status;
        return Result.Success();
    }

    /// <summary>
    /// Updates the planned date range for the stage.
    /// </summary>
    public Result UpdatePlannedDates(FlexibleDateRange? dateRange)
    {
        DateRange = dateRange;
        return Result.Success();
    }

    /// <summary>
    /// Updates the planned date range for the stage, validating that it contains all dated root tasks.
    /// </summary>
    internal Result UpdatePlannedDates(FlexibleDateRange? dateRange, IEnumerable<ProjectTask> rootTasks)
    {
        var rootTaskList = rootTasks.ToList();

        if (dateRange is null)
        {
            if (rootTaskList.Any(t => t.Type == ProjectTaskType.Milestone ? t.PlannedDate.HasValue : t.PlannedDateRange is not null))
            {
                return Result.Failure("A stage cannot be updated to null when it has root tasks with dates.");
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
    /// Updates the progress of the stage.
    /// </summary>
    public Result UpdateProgress(Progress progress)
    {
        Guard.Against.Null(progress, nameof(progress));

        Progress = progress;
        return Result.Success();
    }

    /// <summary>
    /// Updates all role assignments for this stage.
    /// </summary>
    public Result UpdateRoles(Dictionary<ProjectStageRole, HashSet<Guid>> updatedRoles)
    {
        return RoleManager.UpdateRoles(_roles, Id, updatedRoles);
    }

    /// <summary>
    /// Creates a new project stage from a lifecycle stage template.
    /// </summary>
    internal static ProjectStage Create(Guid projectId, ProjectLifecycleStage lifecycleStage)
    {
        Guard.Against.Null(lifecycleStage, nameof(lifecycleStage));

        return new ProjectStage(projectId, lifecycleStage);
    }
}
