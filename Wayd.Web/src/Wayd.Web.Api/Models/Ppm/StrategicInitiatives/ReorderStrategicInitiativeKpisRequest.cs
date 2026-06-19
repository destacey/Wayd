using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands.Kpis;

namespace Wayd.Web.Api.Models.Ppm.StrategicInitiatives;

public sealed record ReorderStrategicInitiativeKpisRequest
{
    /// <summary>
    /// The ordered list of KPI IDs representing the desired display order.
    /// </summary>
    public List<Guid> OrderedKpiIds { get; set; } = [];

    public ReorderStrategicInitiativeKpisCommand ToReorderStrategicInitiativeKpisCommand(Guid strategicInitiativeId)
    {
        return new ReorderStrategicInitiativeKpisCommand(strategicInitiativeId, OrderedKpiIds);
    }
}

public sealed class ReorderStrategicInitiativeKpisRequestValidator : AbstractValidator<ReorderStrategicInitiativeKpisRequest>
{
    public ReorderStrategicInitiativeKpisRequestValidator()
    {
        RuleFor(x => x.OrderedKpiIds)
            .NotEmpty()
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("OrderedKpiIds cannot contain empty GUIDs.");
    }
}
