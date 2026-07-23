using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;

/// <summary>
/// A single portfolio row. Programs, projects and strategic initiatives reference their portfolio by
/// <see cref="Name"/>, so names must be unique within the batch and against existing portfolios.
/// <para>
/// Dates are carried on the row because a portfolio never receives them on creation — <c>Create</c> makes a
/// Proposed portfolio with no date range, and the range is only ever set by the <c>Activate(startDate)</c>
/// and <c>Close(endDate)</c> transitions. The handler replays those transitions with these dates, which is
/// also why they cannot be imported through <c>ActivateProjectPortfolioCommand</c> (it hardcodes today).
/// </para>
/// People are referenced by employee number rather than Id so a batch can be authored without knowing
/// generated Ids.
/// </summary>
public sealed record ImportProjectPortfolioDto(
    string Name,
    string Description,
    ProjectPortfolioStatus Status,
    LocalDate? Start,
    LocalDate? End,
    IReadOnlyList<string> SponsorEmployeeNumbers,
    IReadOnlyList<string> OwnerEmployeeNumbers,
    IReadOnlyList<string> ManagerEmployeeNumbers);

public sealed class ImportProjectPortfolioDtoValidator : CustomValidator<ImportProjectPortfolioDto>
{
    public ImportProjectPortfolioDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(p => p.Description)
            .NotEmpty()
            .MaximumLength(1024);

        RuleFor(p => p.Status)
            .IsInEnum();

        // Mirrors the domain's own construction rules: anything past Proposed needs a start, and anything
        // closed or archived needs both ends of the range.
        RuleFor(p => p.Start)
            .NotNull()
            .When(p => p.Status is not ProjectPortfolioStatus.Proposed)
                .WithMessage("A portfolio that is not proposed must have a Start date.");

        RuleFor(p => p.End)
            .NotNull()
            .When(p => p.Status is ProjectPortfolioStatus.Closed or ProjectPortfolioStatus.Archived)
                .WithMessage("A closed or archived portfolio must have an End date.");

        RuleFor(p => p.End)
            .Must((p, end) => end is null || p.Start is null || p.Start <= end)
                .WithMessage("End date must be on or after the start date.");
    }
}
