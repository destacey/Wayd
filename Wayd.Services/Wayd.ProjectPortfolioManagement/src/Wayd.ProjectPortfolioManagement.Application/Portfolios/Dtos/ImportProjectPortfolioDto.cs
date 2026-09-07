using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;

/// <summary>
/// A single portfolio row. Programs, projects and strategic initiatives reference their portfolio by
/// <see cref="Name"/>, so names must be unique within the batch and against existing portfolios.
/// <para>
/// A portfolio has no planned timeline of its own: <c>Create</c> makes a Proposed portfolio with no date
/// range, and the range is only ever set by the <c>Activate(startDate)</c> and <c>Close(endDate)</c>
/// transitions. So <see cref="ActivatedOn"/> is on the row and the handler replays the activation with it
/// — which is also why it cannot be imported through <c>ActivateProjectPortfolioCommand</c> (that
/// hardcodes today). There is no closing date here: an import cannot close a portfolio, since it can only
/// close once its contents are closed, so the finalize import carries that date on its own row.
/// </para>
/// <para>
/// <see cref="CreatedOn"/> is required on every row but nothing stores it yet: a portfolio records no
/// creation date beyond the <c>SystemCreated</c> audit stamp, which is when the file was uploaded.
/// Requiring it now means the day the portfolio does record one, no file has to change.
/// </para>
/// People are referenced by employee number rather than Id so a batch can be authored without knowing
/// generated Ids.
/// </summary>
public sealed record ImportProjectPortfolioDto(
    string Name,
    string Description,
    ProjectPortfolioStatus Status,
    LocalDate CreatedOn,
    LocalDate? ActivatedOn,
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

        // Mirrors the domain's own construction rules: anything past Proposed has been activated, and
        // anything closed or archived has been closed as well. A date for a state the portfolio never
        // reached is rejected rather than dropped.
        When(p => p.Status is not ProjectPortfolioStatus.Proposed,
            () => RuleFor(p => p.ActivatedOn)
                .NotNull()
                    .WithMessage("A portfolio that is not proposed must have an ActivatedOn date."))
            .Otherwise(() => RuleFor(p => p.ActivatedOn)
                .Empty()
                    .WithMessage("ActivatedOn is only allowed on a portfolio that reached Active."));

        RuleFor(p => p.ActivatedOn)
            .Must((p, activatedOn) => activatedOn is null || p.CreatedOn <= activatedOn)
                .WithMessage("ActivatedOn cannot be earlier than CreatedOn.");
    }
}
