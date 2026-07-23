using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

/// <summary>
/// A single project row. The project's own <see cref="Key"/> is a true natural key (unique in the database)
/// and is what project tasks and strategic initiatives reference. Everything the project points at is
/// referenced by natural key in turn: portfolio, program, expenditure category and lifecycle by name,
/// strategic themes by name, and people by employee number.
/// <para>
/// A project receives its date range on creation, and the <see cref="Status"/> transitions only move the
/// status, so no additional dates are needed on the row.
/// </para>
/// </summary>
public sealed record ImportProjectDto(
    string Name,
    string Description,
    ProjectKey Key,
    ProjectStatus Status,
    string PortfolioName,
    string? ProgramName,
    string ExpenditureCategoryName,
    string? ProjectLifecycleName,
    string? BusinessCase,
    string? ExpectedBenefits,
    LocalDate? Start,
    LocalDate? End,
    IReadOnlyList<string> StrategicThemeNames,
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

        RuleFor(p => p.PortfolioName)
            .NotEmpty();

        RuleFor(p => p.ExpenditureCategoryName)
            .NotEmpty();

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

        // Activating and completing both require a date range; approval requires a lifecycle. Cancelled is
        // exempt from both, since the domain allows cancelling straight from Proposed.
        RuleFor(p => p.Start)
            .NotNull()
            .When(p => p.Status is ProjectStatus.Active or ProjectStatus.Completed)
                .WithMessage("An active or completed project must have a Start and End date.");

        RuleFor(p => p.ProjectLifecycleName)
            .NotEmpty()
            .When(p => p.Status is ProjectStatus.Approved)
                .WithMessage("An approved project must have a project lifecycle.");
    }
}
