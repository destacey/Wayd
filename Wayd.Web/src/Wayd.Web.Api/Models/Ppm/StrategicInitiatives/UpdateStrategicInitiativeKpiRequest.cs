using Wayd.Common.Domain.Models.KeyPerformanceIndicators;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands.Kpis;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

namespace Wayd.Web.Api.Models.Ppm.StrategicInitiatives;

public sealed record UpdateStrategicInitiativeKpiRequest
{
    /// <summary>
    /// The ID of the strategic initiative to which this KPI belongs.
    /// </summary>
    public Guid StrategicInitiativeId { get; set; }

    /// <summary>
    /// The ID of the KPI.
    /// </summary>
    public Guid KpiId { get; set; }

    /// <summary>
    /// The name of the KPI.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// A description of what the KPI measures.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The target value that defines success for the KPI.
    /// </summary>
    public double TargetValue { get; set; }

    /// <summary>
    /// The starting (baseline) value of the KPI.
    /// </summary>
    public double? StartingValue { get; set; }

    /// <summary>
    /// An optional prefix symbol displayed before the numeric value (e.g. "$").
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// An optional suffix symbol displayed after the numeric value (e.g. "%", "M").
    /// </summary>
    public string? Suffix { get; set; }

    /// <summary>
    /// The target direction for the KPI.
    /// </summary>
    public KpiTargetDirection TargetDirection { get; set; }

    public UpdateStrategicInitiativeKpiCommand ToUpdateStrategicInitiativeKpiCommand()
    {
        var parameters = new StrategicInitiativeKpiUpsertParameters(Name, Description, StartingValue, TargetValue, Prefix, Suffix, TargetDirection);

        return new UpdateStrategicInitiativeKpiCommand(StrategicInitiativeId, KpiId, parameters);
    }
}

public sealed class UpdateStrategicInitiativeKpiRequestValidator : AbstractValidator<UpdateStrategicInitiativeKpiRequest>
{
    public UpdateStrategicInitiativeKpiRequestValidator()
    {
        RuleFor(x => x.StrategicInitiativeId)
            .NotEmpty();

        RuleFor(x => x.KpiId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.Description)
            .MaximumLength(512);

        RuleFor(x => x.TargetValue)
            .NotEmpty();

        RuleFor(x => x.Prefix)
            .MaximumLength(8);

        RuleFor(x => x.Suffix)
            .MaximumLength(8);

        RuleFor(x => x.TargetDirection)
            .IsInEnum()
            .WithMessage("A valid KPI direction must be selected.");
    }
}
