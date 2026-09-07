using Wayd.Common.Domain.Models.KeyPerformanceIndicators;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;

/// <summary>
/// A single strategic initiative row, carrying the KPIs that belong to it.
/// </summary>
/// <remarks>
/// The owning portfolio is referenced by id — a name is a display value and two portfolios may share one.
/// Projects are referenced by their keys and people by employee number, which are the natural keys those
/// records actually have.
/// <para>
/// KPIs arrive as a second file whose rows name their parent's import id; the submission boundary groups
/// them onto the initiative row before the run starts, so one import row is one initiative and everything
/// beneath it. Initiatives are imported straight to their final <see cref="Status"/> — unlike programs and
/// portfolios they have nothing beneath them that must close first, so they need no finalization pass.
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
    IReadOnlyList<ImportStrategicInitiativeKpiDto> Kpis);

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

        RuleFor(i => i.Kpis)
            .Must(kpis => kpis.Select(k => k.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == kpis.Count)
                .WithMessage("KPI Name must be unique within a strategic initiative.");

        RuleForEach(i => i.Kpis)
            .NotNull()
            .SetValidator(new ImportStrategicInitiativeKpiDtoValidator());
    }
}

/// <summary>
/// A single KPI row. Its parent is the initiative row it was grouped onto, so it carries no reference of
/// its own.
/// </summary>
public sealed record ImportStrategicInitiativeKpiDto(
    string Name,
    string? Description,
    double TargetValue,
    double? StartingValue,
    string? Prefix,
    string? Suffix,
    KpiTargetDirection TargetDirection);

public sealed class ImportStrategicInitiativeKpiDtoValidator : CustomValidator<ImportStrategicInitiativeKpiDto>
{
    public ImportStrategicInitiativeKpiDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(k => k.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(k => k.Description)
            .MaximumLength(512)
            .When(k => k.Description is not null);

        RuleFor(k => k.Prefix)
            .MaximumLength(8)
            .When(k => k.Prefix is not null);

        RuleFor(k => k.Suffix)
            .MaximumLength(8)
            .When(k => k.Suffix is not null);

        RuleFor(k => k.TargetDirection)
            .IsInEnum();
    }
}
