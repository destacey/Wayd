using Wayd.Common.Domain.Models.KeyPerformanceIndicators;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;

/// <summary>
/// A single strategic initiative row. The owning portfolio is referenced by name, the projects the
/// initiative delivers through by their keys, and people by employee number.
/// <para>
/// Initiatives are imported straight to their final <see cref="Status"/> — unlike programs and portfolios
/// they have nothing beneath them that must close first, so they need no finalization pass.
/// </para>
/// </summary>
public sealed record ImportStrategicInitiativeDto(
    string Name,
    string Description,
    StrategicInitiativeStatus Status,
    string PortfolioName,
    LocalDate Start,
    LocalDate End,
    IReadOnlyList<string> ProjectKeys,
    IReadOnlyList<string> SponsorEmployeeNumbers,
    IReadOnlyList<string> OwnerEmployeeNumbers);

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

        RuleFor(i => i.PortfolioName)
            .NotEmpty();

        RuleFor(i => i.End)
            .GreaterThanOrEqualTo(i => i.Start)
                .WithMessage("End date must be on or after the start date.");
    }
}

/// <summary>
/// A single KPI row, attached to its initiative by <see cref="StrategicInitiativeName"/>. KPIs are imported
/// separately from initiatives because an initiative has many of them, which a single flat row cannot carry.
/// </summary>
public sealed record ImportStrategicInitiativeKpiDto(
    string StrategicInitiativeName,
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

        RuleFor(k => k.StrategicInitiativeName)
            .NotEmpty();

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
