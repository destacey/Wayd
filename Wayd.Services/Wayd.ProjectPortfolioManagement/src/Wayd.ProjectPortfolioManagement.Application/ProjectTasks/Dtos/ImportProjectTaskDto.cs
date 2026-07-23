using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;

/// <summary>
/// A single project task row. The task's own <see cref="Name"/> is its natural key within its project, so
/// child rows can name their parent: <see cref="ParentTaskName"/> when nesting under another task, or
/// <see cref="PhaseName"/> alone for a task at the root of a phase. Phases come from the lifecycle assigned
/// to the project, so the project must already have one.
/// <para>
/// A milestone carries a single <see cref="PlannedDate"/> and no progress; a task carries a planned range
/// and a progress value. The domain enforces both, and the validator mirrors them so a bad row is rejected
/// before the batch is applied.
/// </para>
/// </summary>
public sealed record ImportProjectTaskDto(
    ProjectKey ProjectKey,
    string Name,
    string? Description,
    ProjectTaskType Type,
    TaskStatus Status,
    TaskPriority Priority,
    string PhaseName,
    string? ParentTaskName,
    decimal? Progress,
    LocalDate? PlannedStart,
    LocalDate? PlannedEnd,
    LocalDate? PlannedDate,
    decimal? EstimatedEffortHours,
    IReadOnlyList<string> AssigneeEmployeeNumbers);

public sealed class ImportProjectTaskDtoValidator : CustomValidator<ImportProjectTaskDto>
{
    public ImportProjectTaskDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(t => t.ProjectKey)
            .NotNull();

        RuleFor(t => t.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(t => t.Description)
            .MaximumLength(2048);

        RuleFor(t => t.Type)
            .IsInEnum();

        RuleFor(t => t.Status)
            .IsInEnum();

        RuleFor(t => t.Priority)
            .IsInEnum();

        RuleFor(t => t.PhaseName)
            .NotEmpty();

        RuleFor(t => t.EstimatedEffortHours)
            .GreaterThan(0)
            .When(t => t.EstimatedEffortHours.HasValue);

        When(t => t.Type is ProjectTaskType.Milestone, () =>
        {
            RuleFor(t => t.PlannedDate)
                .NotNull()
                    .WithMessage("A milestone must have a PlannedDate.");

            RuleFor(t => t.PlannedStart)
                .Null()
                    .WithMessage("A milestone cannot have a planned date range.");

            RuleFor(t => t.PlannedEnd)
                .Null()
                    .WithMessage("A milestone cannot have a planned date range.");

            RuleFor(t => t.Progress)
                .Null()
                    .WithMessage("A milestone cannot have progress.");
        }).Otherwise(() =>
        {
            RuleFor(t => t.PlannedDate)
                .Null()
                    .WithMessage("A task cannot have a single planned date. Use PlannedStart and PlannedEnd instead.");

            RuleFor(t => t.Progress)
                .NotNull()
                    .WithMessage("A task must have progress.")
                .InclusiveBetween(0m, 100m);

            RuleFor(t => t)
                .Must(t => (t.PlannedStart is null && t.PlannedEnd is null) || (t.PlannedStart is not null && t.PlannedEnd is not null))
                    .WithMessage("PlannedStart and PlannedEnd must either both be empty or both have a value.");

            RuleFor(t => t.PlannedEnd)
                .Must((t, end) => end is null || t.PlannedStart is null || t.PlannedStart <= end)
                    .WithMessage("PlannedEnd must be on or after PlannedStart.");
        });
    }
}
