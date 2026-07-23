using NodaTime.Extensions;
using Wayd.Common.Domain.Models.KeyPerformanceIndicators;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.Web.Api.Models.Ppm.StrategicInitiatives;

/// <summary>
/// A single CSV row for the strategic initiative import. The owning portfolio is referenced by name and
/// the delivering projects by a semicolon-separated list of their keys; role columns hold semicolon-
/// separated employee numbers. <see cref="Status"/> is the status the initiative should end up in
/// (case-insensitive), reached by replaying the real lifecycle transitions.
/// </summary>
public sealed class ImportStrategicInitiativeRequest
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string PortfolioName { get; set; } = default!;

    /// <summary>The initiative's status. Defaults to Active when the column is absent.</summary>
    public string Status { get; set; } = nameof(StrategicInitiativeStatus.Active);

    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    /// <summary>Semicolon-separated project keys the initiative delivers through.</summary>
    public string? ProjectKeys { get; set; }

    public string? Sponsors { get; set; }
    public string? Owners { get; set; }

    public ImportStrategicInitiativeDto ToImportStrategicInitiativeDto()
    {
        var status = Enum.Parse<StrategicInitiativeStatus>(Status.Trim(), ignoreCase: true);

        return new ImportStrategicInitiativeDto(
            Name,
            Description,
            status,
            PortfolioName,
            Start.ToLocalDateTime().Date,
            End.ToLocalDateTime().Date,
            CsvList.Split(ProjectKeys),
            CsvList.Split(Sponsors),
            CsvList.Split(Owners));
    }
}

public sealed class ImportStrategicInitiativeRequestValidator : CustomValidator<ImportStrategicInitiativeRequest>
{
    public ImportStrategicInitiativeRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(i => i.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(i => i.Description)
            .NotEmpty()
            .MaximumLength(2048);

        RuleFor(i => i.PortfolioName)
            .NotEmpty();

        RuleFor(i => i.Status)
            .NotEmpty()
            .Must(s => Enum.TryParse<StrategicInitiativeStatus>(s.Trim(), ignoreCase: true, out _))
                .WithMessage("Status must be one of 'Proposed', 'Approved', 'Active', 'Completed' or 'Cancelled'.");

        RuleFor(i => i.Start)
            .NotEmpty();

        RuleFor(i => i.End)
            .NotEmpty()
            .GreaterThanOrEqualTo(i => i.Start)
                .WithMessage("End date must be on or after the start date.");
    }
}

/// <summary>
/// A single CSV row for the strategic initiative KPI import, attached to its initiative by name. KPIs are
/// a separate file because an initiative has many of them, which a single initiative row cannot carry.
/// </summary>
public sealed class ImportStrategicInitiativeKpiRequest
{
    public string StrategicInitiativeName { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public double TargetValue { get; set; }
    public double? StartingValue { get; set; }

    /// <summary>A symbol shown before the value, such as "$".</summary>
    public string? Prefix { get; set; }

    /// <summary>A symbol shown after the value, such as "%".</summary>
    public string? Suffix { get; set; }

    /// <summary>Whether success means increasing or decreasing the value. Defaults to Increase.</summary>
    public string TargetDirection { get; set; } = nameof(KpiTargetDirection.Increase);

    public ImportStrategicInitiativeKpiDto ToImportStrategicInitiativeKpiDto()
    {
        var direction = Enum.Parse<KpiTargetDirection>(TargetDirection.Trim(), ignoreCase: true);

        return new ImportStrategicInitiativeKpiDto(
            StrategicInitiativeName,
            Name,
            Description,
            TargetValue,
            StartingValue,
            Prefix,
            Suffix,
            direction);
    }
}

public sealed class ImportStrategicInitiativeKpiRequestValidator : CustomValidator<ImportStrategicInitiativeKpiRequest>
{
    public ImportStrategicInitiativeKpiRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(k => k.StrategicInitiativeName)
            .NotEmpty();

        RuleFor(k => k.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(k => k.Description)
            .MaximumLength(512);

        RuleFor(k => k.Prefix)
            .MaximumLength(8);

        RuleFor(k => k.Suffix)
            .MaximumLength(8);

        RuleFor(k => k.TargetDirection)
            .NotEmpty()
            .Must(d => Enum.TryParse<KpiTargetDirection>(d.Trim(), ignoreCase: true, out _))
                .WithMessage("TargetDirection must be either 'Increase' or 'Decrease'.");
    }
}
