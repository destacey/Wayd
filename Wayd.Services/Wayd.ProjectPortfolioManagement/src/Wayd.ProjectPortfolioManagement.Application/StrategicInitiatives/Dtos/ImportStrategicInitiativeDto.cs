using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;

/// <summary>
/// A single strategic initiative row.
/// </summary>
/// <remarks>
/// The owning portfolio is referenced by id — a name is a display value and two portfolios may share one.
/// Projects are referenced by their keys and people by employee number, which are the natural keys those
/// records actually have.
/// <para>
/// Initiatives are imported straight to their final <see cref="Status"/> — unlike programs and portfolios
/// they have nothing beneath them that must close first, so they need no finalization pass. Their KPIs go
/// through the KPI import, which accepts a closed initiative's KPIs as it does a closed project's.
/// </para>
/// </remarks>
public sealed record ImportStrategicInitiativeDto(
    string Name,
    string Description,
    StrategicInitiativeStatus Status,
    Guid PortfolioId,
    LocalDate Start,
    LocalDate End,
    IReadOnlyList<string> ProjectKeys,
    IReadOnlyList<string> SponsorEmployeeNumbers,
    IReadOnlyList<string> OwnerEmployeeNumbers,
    string? Links = null);

public sealed class ImportStrategicInitiativeDtoValidator : CustomValidator<ImportStrategicInitiativeDto>
{
    public ImportStrategicInitiativeDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(i => i.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(i => i.Description)
            .NotEmpty()
            .MaximumLength(2048);

        RuleFor(i => i.Status)
            .IsInEnum();

        RuleFor(i => i.PortfolioId)
            .NotEmpty();

        RuleFor(i => i.End)
            .GreaterThanOrEqualTo(i => i.Start)
                .WithMessage("End date must be on or after the start date.");
    }
}
