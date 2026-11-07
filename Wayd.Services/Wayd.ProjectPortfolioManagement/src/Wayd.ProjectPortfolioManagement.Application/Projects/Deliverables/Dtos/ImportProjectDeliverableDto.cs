using Wayd.Common.Domain.Models.ProjectPortfolioManagement;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Dtos;

/// <summary>
/// One deliverable to add to an existing project. Rows for one project are added in file order, which becomes
/// their order.
/// </summary>
/// <param name="ProjectKey">The project, by key, like the other PPM imports.</param>
/// <param name="OwnerEmployeeNumber">The accountable employee, by employee number.</param>
/// <param name="Status">
/// A status of the deliverable workflow, by its name or by its meaning (NotStarted, InProgress, Accepted,
/// Descoped); blank for Not Started. A completed or canceled project accepts only Accepted, Descoped or another
/// closed status.
/// </param>
/// <param name="StatusChangedAt">When it moved into <paramref name="Status"/>; blank for when the import runs.</param>
/// <param name="StatusReason">Why it is in that status. Required for Descoped and any other Removed status.</param>
public sealed record ImportProjectDeliverableDto(
    ProjectKey ProjectKey,
    string Name,
    string? Description,
    string? AcceptanceCriteria,
    string? OwnerEmployeeNumber,
    LocalDate? TargetDate,
    string? Status,
    Instant? StatusChangedAt,
    string? StatusReason);

public sealed class ImportProjectDeliverableDtoValidator : CustomValidator<ImportProjectDeliverableDto>
{
    public ImportProjectDeliverableDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(d => d.ProjectKey).NotNull();
        RuleFor(d => d.Name).NotEmpty().MaximumLength(256);
        RuleFor(d => d.Description).MaximumLength(4096);
        RuleFor(d => d.AcceptanceCriteria).MaximumLength(4096);
        RuleFor(d => d.OwnerEmployeeNumber).MaximumLength(256);
        RuleFor(d => d.Status).MaximumLength(64);
        RuleFor(d => d.StatusReason).MaximumLength(1024);
        RuleFor(d => d.StatusChangedAt)
            .Null()
                .When(d => string.IsNullOrWhiteSpace(d.Status))
                .WithMessage("StatusChangedAt needs a Status.");
    }
}
