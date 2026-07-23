using NodaTime.Extensions;
using Wayd.Common.Domain.Extensions.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.Web.Api.Models.Ppm.ProjectTasks;

/// <summary>
/// A single CSV row for the project task import. The project is referenced by key and the phase by name
/// (phases come from the project's assigned lifecycle). A task nests under another by naming it in
/// <see cref="ParentTaskName"/>; leaving that empty makes the task a root task of its phase. Rows may be
/// listed in any order — parents are applied before children.
/// </summary>
public sealed class ImportProjectTaskRequest
{
    public string ProjectKey { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string PhaseName { get; set; } = default!;

    /// <summary>The task this one nests under. Empty makes it a root task of its phase.</summary>
    public string? ParentTaskName { get; set; }

    /// <summary>'Task' or 'Milestone'. Defaults to Task when the column is absent.</summary>
    public string Type { get; set; } = nameof(ProjectTaskType.Task);

    public string Status { get; set; } = nameof(TaskStatus.NotStarted);
    public string Priority { get; set; } = nameof(TaskPriority.Medium);

    /// <summary>Percent complete (0-100). Required for tasks, not allowed for milestones.</summary>
    public decimal? Progress { get; set; }

    /// <summary>Planned start, for tasks. Milestones use PlannedDate instead.</summary>
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }

    /// <summary>The milestone's date. Only for milestones.</summary>
    public DateTime? PlannedDate { get; set; }

    public decimal? EstimatedEffortHours { get; set; }

    /// <summary>Semicolon-separated employee numbers assigned to the task.</summary>
    public string? Assignees { get; set; }

    public ImportProjectTaskDto ToImportProjectTaskDto()
    {
        var type = Enum.Parse<ProjectTaskType>(Type.Trim(), ignoreCase: true);
        var status = Enum.Parse<TaskStatus>(Status.Trim(), ignoreCase: true);
        var priority = Enum.Parse<TaskPriority>(Priority.Trim(), ignoreCase: true);

        return new ImportProjectTaskDto(
            new ProjectKey(ProjectKey),
            Name,
            Description,
            type,
            status,
            priority,
            PhaseName,
            string.IsNullOrWhiteSpace(ParentTaskName) ? null : ParentTaskName,
            Progress,
            PlannedStart?.ToLocalDateTime().Date,
            PlannedEnd?.ToLocalDateTime().Date,
            PlannedDate?.ToLocalDateTime().Date,
            EstimatedEffortHours,
            CsvList.Split(Assignees));
    }
}

public sealed class ImportProjectTaskRequestValidator : CustomValidator<ImportProjectTaskRequest>
{
    public ImportProjectTaskRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.ProjectKey)
            .NotEmpty()
            .Must(k => k.Trim().IsValidProjectKeyFormat())
                .WithMessage("Invalid project key format. Project keys are uppercase letters and numbers only, 2-20 characters.");

        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(t => t.Description)
            .MaximumLength(2048);

        RuleFor(t => t.PhaseName)
            .NotEmpty();

        RuleFor(t => t.Type)
            .NotEmpty()
            .Must(t => Enum.TryParse<ProjectTaskType>(t.Trim(), ignoreCase: true, out _))
                .WithMessage("Type must be either 'Task' or 'Milestone'.");

        RuleFor(t => t.Status)
            .NotEmpty()
            .Must(s => Enum.TryParse<TaskStatus>(s.Trim(), ignoreCase: true, out _))
                .WithMessage("Status is not a valid task status.");

        RuleFor(t => t.Priority)
            .NotEmpty()
            .Must(p => Enum.TryParse<TaskPriority>(p.Trim(), ignoreCase: true, out _))
                .WithMessage("Priority is not a valid task priority.");

        RuleFor(t => t.Progress)
            .InclusiveBetween(0m, 100m)
            .When(t => t.Progress.HasValue);

        RuleFor(t => t.EstimatedEffortHours)
            .GreaterThan(0)
            .When(t => t.EstimatedEffortHours.HasValue);

        RuleFor(t => t.PlannedEnd)
            .Must((t, end) => end is null || t.PlannedStart is null || t.PlannedStart <= end)
                .WithMessage("PlannedEnd must be on or after PlannedStart.");
    }
}
