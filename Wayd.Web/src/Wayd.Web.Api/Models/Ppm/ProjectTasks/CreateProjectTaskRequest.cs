using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.Web.Api.Models.Ppm.ProjectTasks;

public sealed record CreateProjectTaskRequest
{
    /// <summary>
    /// The name of the task.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// A detailed description of the task (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The type of task (Task or Milestone).
    /// </summary>
    public int TypeId { get; set; }

    /// <summary>
    /// The current status of the task.
    /// </summary>
    public int StatusId { get; set; }

    /// <summary>
    /// The priority level of the task.
    /// </summary>
    public int PriorityId { get; set; }

    /// <summary>
    /// The assignees of the project task.
    /// </summary>
    public List<Guid>? AssigneeIds { get; set; } = [];

    /// <summary>
    /// The progress of the task (optional). Ranges from 0.0 to 100.0. Milestones can not update progress directly.
    /// </summary>
    public decimal? Progress { get; set; }

    /// <summary>
    /// The ID of the parent phase or task. If it matches a phase, the task becomes a root task in that phase.
    /// If it matches a task, the new task becomes a child of that task and inherits the phase.
    /// </summary>
    public Guid ParentId { get; set; }

    /// <summary>
    /// The planned start date for the task (for tasks, not milestones).
    /// </summary>
    public LocalDate? PlannedStart { get; set; }

    /// <summary>
    /// The planned end date for the task (for tasks, not milestones).
    /// </summary>
    public LocalDate? PlannedEnd { get; set; }

    /// <summary>
    /// The planned date for a milestone (for milestones only).
    /// </summary>
    public LocalDate? PlannedDate { get; set; }

    /// <summary>
    /// The estimated effort in hours (optional).
    /// </summary>
    public decimal? EstimatedEffortHours { get; set; }

    public CreateProjectTaskCommand ToCreateProjectTaskCommand(Guid projectId)
    {
        var plannedDateRange = PlannedStart is null || PlannedEnd is null
            ? null
            : new FlexibleDateRange((LocalDate)PlannedStart, (LocalDate)PlannedEnd);

        return new CreateProjectTaskCommand(
            projectId,
            Name,
            Description,
            (ProjectTaskType)TypeId,
            (ProjectPortfolioManagement.Domain.Enums.TaskStatus)StatusId,
            (TaskPriority)PriorityId,
            Progress.HasValue ? new Progress(Progress.Value) : null,
            ParentId,
            plannedDateRange,
            PlannedDate,
            EstimatedEffortHours,
            AssigneeIds
        );
    }
}

public sealed class CreateProjectTaskRequestValidator : CustomValidator<CreateProjectTaskRequest>
{
    public CreateProjectTaskRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .MaximumLength(2048);

        RuleFor(x => x.TypeId)
            .Must(type => Enum.IsDefined(typeof(ProjectTaskType), type))
            .WithMessage("Invalid task type.");

        RuleFor(x => x.StatusId)
            .Must(status => Enum.IsDefined(typeof(ProjectPortfolioManagement.Domain.Enums.TaskStatus), status))
            .WithMessage("Invalid task status.");

        RuleFor(x => x.PriorityId)
            .Must(priority => Enum.IsDefined(typeof(TaskPriority), priority))
            .WithMessage("Invalid task priority.");

        RuleFor(x => x.AssigneeIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("AssigneeIds cannot contain empty GUIDs.");

        RuleFor(x => x.Progress)
            .NotNull()
            .When(x => x.TypeId == (int)ProjectTaskType.Task)
            .WithMessage("Progress is required for tasks.");

        RuleFor(x => x.Progress)
            .Null()
            .When(x => x.TypeId == (int)ProjectTaskType.Milestone)
            .WithMessage("Progress is not applicable for milestones.");

        RuleFor(x => x.ParentId)
            .NotEmpty()
            .WithMessage("ParentId is required and cannot be an empty GUID.");

        // Milestone-specific validations
        RuleFor(x => x.PlannedDate)
            .NotNull()
            .When(x => x.TypeId == (int)ProjectTaskType.Milestone)
            .WithMessage("PlannedDate is required for milestones.");

        RuleFor(x => x.PlannedStart)
            .Must((request, plannedStart) => request.TypeId != (int)ProjectTaskType.Milestone || (request.PlannedStart == null && request.PlannedEnd == null))
            .WithMessage("Milestones cannot have a planned date range.");

        // Task-specific validations
        RuleFor(x => x.PlannedDate)
            .Null()
            .When(x => x.TypeId == (int)ProjectTaskType.Task)
            .WithMessage("Tasks cannot have a single planned date. Use PlannedStart and PlannedEnd instead.");

        RuleFor(x => x.PlannedStart)
            .Must((request, plannedStart) => (request.PlannedStart == null && request.PlannedEnd == null) || (request.PlannedStart != null && request.PlannedEnd != null))
            .When(x => x.TypeId == (int)ProjectTaskType.Task)
            .WithMessage("PlannedStart and PlannedEnd must either both be null or both have a value.");

        RuleFor(x => x.PlannedEnd)
            .Must((request, plannedEnd) => request.PlannedStart == null || request.PlannedEnd == null || request.PlannedStart <= request.PlannedEnd)
            .When(x => x.TypeId == (int)ProjectTaskType.Task)
            .WithMessage("PlannedEnd must be greater than or equal to PlannedStart.");

        RuleFor(x => x.EstimatedEffortHours)
            .GreaterThan(0)
            .When(x => x.EstimatedEffortHours.HasValue);
    }
}
