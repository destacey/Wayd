using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.Risks.Dtos;

/// <summary>
/// One risk on its way in.
/// </summary>
/// <remarks>
/// Carries no import id: the platform holds the caller's key on the row rather than in the payload, so
/// there is only one of it and results are reported against that.
/// </remarks>
public sealed record ImportRiskDto(
    string Summary,
    string? Description,
    Guid TeamId,
    Instant ReportedOn,
    Guid ReportedById,
    RiskStatus Status,
    RiskCategory Category,
    RiskGrade Impact,
    RiskGrade Likelihood,
    Guid? AssigneeId,
    LocalDate? FollowUpDate,
    string? Response,
    Instant? ClosedDate);

public sealed class ImportRiskDtoValidator : CustomValidator<ImportRiskDto>
{
    public ImportRiskDtoValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.TeamId)
            .NotEmpty();

        RuleFor(r => r.Summary)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(r => r.Description)
            .MaximumLength(1024);

        RuleFor(r => r.ReportedOn)
            .NotEmpty()
            .Must(date => date < dateTimeProvider.Now)
            .WithMessage("The ReportedOnUtc date must be less than the current UTC date and time.");

        RuleFor(r => r.ReportedById)
            .NotEmpty();

        RuleFor(e => e.Status)
            .IsInEnum()
            .WithMessage("A valid status must be selected.");

        RuleFor(e => e.Category)
            .IsInEnum()
            .WithMessage("A valid category must be selected.");

        RuleFor(e => e.Impact)
            .IsInEnum()
            .WithMessage("A valid impact must be selected.");

        RuleFor(e => e.Likelihood)
            .IsInEnum()
            .WithMessage("A valid likelihood must be selected.");

        RuleFor(r => r.Response)
            .MaximumLength(1024);

        When(r => r.Status == RiskStatus.Closed,
            () => RuleFor(r => r.ClosedDate)
                .NotEmpty()
                    .WithMessage("The ClosedDateUtc can not be empty if the status is Closed.")
                .Must(date => date < dateTimeProvider.Now)
                    .WithMessage("The ClosedDateUtc date must be less than the current UTC date and time.")
                .Must((model, date) => date > model.ReportedOn)
                    .WithMessage("The ClosedDateUtc date must be greater than the ReportedOnUtc date and time."))
            .Otherwise(() => RuleFor(r => r.ClosedDate)
                .Empty()
                    .WithMessage("The ClosedDateUtc must be empty if the status is not Closed."));
    }
}

