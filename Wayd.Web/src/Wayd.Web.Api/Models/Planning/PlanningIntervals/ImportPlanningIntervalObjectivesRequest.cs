using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Domain.Enums;
using NodaTime.Extensions;

namespace Wayd.Web.Api.Models.Planning.PlanningIntervals;

public class ImportPlanningIntervalObjectivesRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it. Falls back to the row's position when the column is absent, so a
    /// hand-authored file still works.
    /// </summary>
    public string? ImportId { get; set; }

    public Guid PlanningIntervalId { get; set; }
    public Guid TeamId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public int StatusId { get; set; }
    public double Progress { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? TargetDate { get; set; }
    public bool IsStretch { get; set; }
    public DateTime? ClosedDateUtc { get; set; }
    public int? Order { get; set; }

    public ImportPlanningIntervalObjectiveDto ToImportPlanningIntervalObjectiveDto()
    {
        LocalDate? startDate = StartDate?.ToLocalDateTime().Date;
        LocalDate? targetDate = TargetDate?.ToLocalDateTime().Date;
        Instant? closedDateUtc = ClosedDateUtc.HasValue ? Instant.FromDateTimeUtc(DateTime.SpecifyKind(ClosedDateUtc.Value, DateTimeKind.Utc)) : null;
        return new ImportPlanningIntervalObjectiveDto(PlanningIntervalId, TeamId, Name, Description, (ObjectiveStatus)StatusId, Progress, startDate, targetDate, IsStretch, closedDateUtc, Order);
    }
}

public sealed class ImportPlanningIntervalObjectivesRequestValidator : CustomValidator<ImportPlanningIntervalObjectivesRequest>
{
    public ImportPlanningIntervalObjectivesRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(o => o.TeamId)
            .NotEmpty()
            .WithMessage("A plan must be selected.");

        RuleFor(o => o.Name)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(o => o.Description)
            .MaximumLength(1024);

        RuleFor(o => (ObjectiveStatus)o.StatusId)
            .IsInEnum()
            .WithMessage("A valid status must be selected.");

        RuleFor(o => o.Progress)
            .InclusiveBetween(0.0d, 100.0d)
            .WithMessage("The progress must be between 0 and 100.");

        When(o => o.StartDate.HasValue && o.TargetDate.HasValue, () =>
        {
            RuleFor(o => o.StartDate)
                .LessThan(o => o.TargetDate)
                .WithMessage("The start date must be before the target date.");
        });

        When(o => (ObjectiveStatus)o.StatusId is ObjectiveStatus.Completed or ObjectiveStatus.Canceled or ObjectiveStatus.Missed,
            () => RuleFor(o => o.ClosedDateUtc)
                .NotEmpty()
                    .WithMessage("The ClosedDateUtc can not be empty if the status is Completed or Canceled."))
            .Otherwise(() => RuleFor(o => o.ClosedDateUtc)
                .Empty()
                    .WithMessage("The ClosedDateUtc must be empty if the status is not Completed or Canceled"));
    }
}
