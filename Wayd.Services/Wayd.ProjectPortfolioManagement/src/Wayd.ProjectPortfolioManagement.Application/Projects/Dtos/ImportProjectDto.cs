using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

/// <summary>
/// A single project row.
/// </summary>
/// <remarks>
/// The project's own <see cref="Key"/> is a true natural key (unique in the database) and is what project
/// tasks and strategic initiatives reference. What the project points at is referenced by id — portfolio,
/// program, expenditure category, lifecycle and strategic themes all have names that are not uniquely
/// indexed, so a name is a display value that may match more than one record. People keep their employee
/// number, which is the natural key an employee actually has.
/// <para>
/// A project receives its date range on creation, and the <see cref="Status"/> transitions only move the
/// status, so no additional dates are needed on the row.
/// </para>
/// </remarks>
public sealed record ImportProjectDto(
    string Name,
    string Description,
    ProjectKey Key,
    ProjectStatus Status,
    Guid PortfolioId,
    Guid? ProgramId,
    int ExpenditureCategoryId,
    Guid? ProjectLifecycleId,
    string? BusinessCase,
    string? ExpectedBenefits,
    LocalDate? Start,
    LocalDate? End,
    IReadOnlyList<Guid> StrategicThemeIds,
    IReadOnlyList<string> SponsorEmployeeNumbers,
    IReadOnlyList<string> OwnerEmployeeNumbers,
    IReadOnlyList<string> ManagerEmployeeNumbers,
    IReadOnlyList<string> MemberEmployeeNumbers);

public sealed class ImportProjectDtoValidator : CustomValidator<ImportProjectDto>
{
    public ImportProjectDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(p => p.Description)
            .NotEmpty()
            .MaximumLength(4096);

        RuleFor(p => p.Key)
            .NotNull();

        RuleFor(p => p.Status)
            .IsInEnum();

        RuleFor(p => p.PortfolioId)
            .NotEmpty();

        RuleFor(p => p.ExpenditureCategoryId)
            .GreaterThan(0);

        RuleFor(p => p.BusinessCase)
            .MaximumLength(4096);

        RuleFor(p => p.ExpectedBenefits)
            .MaximumLength(4096);

        RuleFor(p => p)
            .Must(p => (p.Start is null && p.End is null) || (p.Start is not null && p.End is not null))
                .WithMessage("Start and End must either both be empty or both have a value.");

        RuleFor(p => p.End)
            .Must((p, end) => end is null || p.Start is null || p.Start <= end)
                .WithMessage("End date must be on or after the start date.");

        // Activating and completing both require a date range; approval requires a lifecycle. Canceled is
        // exempt from both, since the domain allows canceling straight from Proposed.
        RuleFor(p => p.Start)
            .NotNull()
            .When(p => p.Status is ProjectStatus.Active or ProjectStatus.Completed)
                .WithMessage("An active or completed project must have a Start and End date.");

        RuleFor(p => p.ProjectLifecycleId)
            .NotNull()
            .When(p => p.Status is ProjectStatus.Approved)
                .WithMessage("An approved project must have a project lifecycle.");
    }
}
