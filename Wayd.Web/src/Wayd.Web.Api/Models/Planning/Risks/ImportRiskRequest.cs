using Wayd.Common.Application.Interfaces;
using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Planning.Domain.Enums;
using NodaTime.Extensions;

namespace Wayd.Web.Api.Models.Planning.Risks;

public class ImportRiskRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it. Falls back to the row's position when the column is absent, so a
    /// hand-authored file still works.
    /// </summary>
    public string? ImportId { get; set; }
    public Guid TeamId { get; set; }
    public string Summary { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime ReportedOnUtc { get; set; }
    public Guid ReportedById { get; set; }
    public int StatusId { get; set; }
    public int CategoryId { get; set; }
    public int ImpactId { get; set; }
    public int LikelihoodId { get; set; }
    public Guid? AssigneeId { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public string? Response { get; set; }
    public DateTime? ClosedDateUtc { get; set; }

    public ImportRiskDto ToImportRiskDto()
    {
        Instant reportedOn = Instant.FromDateTimeUtc(DateTime.SpecifyKind(ReportedOnUtc, DateTimeKind.Utc));
        Instant? closedDate = ClosedDateUtc.HasValue ? Instant.FromDateTimeUtc(DateTime.SpecifyKind(ClosedDateUtc.Value, DateTimeKind.Utc)) : null;
        LocalDate? followUpDate = FollowUpDate?.ToLocalDateTime().Date;

        return new ImportRiskDto(Summary, Description, TeamId, reportedOn, ReportedById, (RiskStatus)StatusId, (RiskCategory)CategoryId, (RiskGrade)ImpactId, (RiskGrade)LikelihoodId, AssigneeId, followUpDate, Response, closedDate);
    }
}

public sealed class ImportRiskRequestValidator : CustomValidator<ImportRiskRequest>
{
    public ImportRiskRequestValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.TeamId)
            .NotEmpty();

        RuleFor(r => r.Summary)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(r => r.Description)
            .MaximumLength(1024);

        RuleFor(r => r.ReportedOnUtc)
            .NotEmpty()
            .Must(date => date < dateTimeProvider.Now.ToDateTimeUtc())
            .WithMessage("The ReportedOnUtc date must be less than the current UTC date and time.");

        RuleFor(r => r.ReportedById)
            .NotEmpty();

        RuleFor(r => (RiskStatus)r.StatusId)
            .IsInEnum()
            .WithMessage("A valid status must be selected.");

        RuleFor(r => (RiskCategory)r.CategoryId)
            .IsInEnum()
            .WithMessage("A valid category must be selected.");

        RuleFor(r => (RiskGrade)r.ImpactId)
            .IsInEnum()
            .WithMessage("A valid impact must be selected.");

        RuleFor(r => (RiskGrade)r.LikelihoodId)
            .IsInEnum()
            .WithMessage("A valid likelihood must be selected.");

        RuleFor(r => r.Response)
            .MaximumLength(1024);

        When(r => (RiskStatus)r.StatusId == RiskStatus.Closed,
            () => RuleFor(r => r.ClosedDateUtc)
                .NotEmpty()
                    .WithMessage("The ClosedDateUtc can not be empty if the status is Closed.")
                .Must(date => date < dateTimeProvider.Now.ToDateTimeUtc())
                    .WithMessage("The ClosedDateUtc date must be less than the current UTC date and time.")
                .Must((model, date) => date > model.ReportedOnUtc)
                    .WithMessage("The ClosedDateUtc date must be greater than the ReportedOnUtc date and time."))
            .Otherwise(() => RuleFor(r => r.ClosedDateUtc)
                .Empty()
                    .WithMessage("The ClosedDateUtc must be empty if the status is not Closed."));
    }
}
