using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.Web.Api.Models.Ppm.ProjectTasks;

public sealed record UpdateProjectTaskRequest
{
    /// <summary>
    /// The ID of the task.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The name of the task.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// A detailed description of the task (optional).
    /// </summary>
    public string? Description { get; set; }

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
    /// The ID of the parent phase or task.
    /// </summary>
    public Guid ParentId { get; set; }

    /// <summary>
    /// The planned start date for the task.
    /// </summary>
    public LocalDate? PlannedStart { get; set; }

    /// <summary>
    /// The planned end date for the task.
    /// </summary>
    public LocalDate? PlannedEnd { get; set; }

    /// <summary>
    /// The planned date for a milestone.
    /// </summary>
    public LocalDate? PlannedDate { get; set; }

    /// <summary>
    /// The estimated effort in hours (optional).
    /// </summary>
    public decimal? EstimatedEffortHours { get; set; }

    /// <summary>
    /// Creates an UpdateProjectTaskRequest from a ProjectTaskDto.
    /// </summary>
    public static UpdateProjectTaskRequest FromDto(ProjectTaskDto dto)
    {
        return new UpdateProjectTaskRequest
        {
            Id = dto.Id,
            Name = dto.Name,
            Description = dto.Description,
            StatusId = dto.Status.Id,
            PriorityId = dto.Priority.Id,
            Progress = dto.Progress,
            ParentId = dto.ParentId ?? dto.ProjectPhaseId,
            PlannedStart = dto.PlannedStart,
            PlannedEnd = dto.PlannedEnd,
            PlannedDate = dto.PlannedDate,
            EstimatedEffortHours = dto.EstimatedEffortHours,
            AssigneeIds = [.. dto.Assignees.Select(a => a.Id)]
        };
    }

    public UpdateProjectTaskCommand ToUpdateProjectTaskCommand()
    {
        var plannedDateRange = PlannedStart is null || PlannedEnd is null
            ? null
            : new FlexibleDateRange((LocalDate)PlannedStart, (LocalDate)PlannedEnd);

        return new UpdateProjectTaskCommand(
            Id,
            Name,
            Description,
            (TaskStatus)StatusId,
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

public sealed class UpdateProjectTaskRequestValidator : CustomValidator<UpdateProjectTaskRequest>
{
    public UpdateProjectTaskRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .MaximumLength(2048);

        RuleFor(x => x.StatusId)
            .Must(status => Enum.IsDefined(typeof(TaskStatus), status))
            .WithMessage("Invalid task status.");

        RuleFor(x => x.PriorityId)
            .Must(priority => Enum.IsDefined(typeof(TaskPriority), priority))
            .WithMessage("Invalid task priority.");

        RuleFor(x => x.AssigneeIds)
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("AssigneeIds cannot contain empty GUIDs.");

        RuleFor(x => x.PlannedStart)
            .Must((request, plannedStart) => (request.PlannedStart == null && request.PlannedEnd == null) || (request.PlannedStart != null && request.PlannedEnd != null))
            .WithMessage("PlannedStart and PlannedEnd must either both be null or both have a value.");

        RuleFor(x => x.PlannedEnd)
            .Must((request, plannedEnd) => request.PlannedStart == null || request.PlannedEnd == null || request.PlannedStart <= request.PlannedEnd)
            .WithMessage("PlannedEnd must be greater than or equal to PlannedStart.");

        RuleFor(x => x.EstimatedEffortHours)
            .GreaterThan(0)
            .When(x => x.EstimatedEffortHours.HasValue);
    }
}
