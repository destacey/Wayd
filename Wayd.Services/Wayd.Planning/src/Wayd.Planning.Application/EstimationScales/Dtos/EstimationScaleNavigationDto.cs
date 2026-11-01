using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.EstimationScales.Dtos;

/// <summary>
/// Identifies an estimation scale from a record that uses it.
/// </summary>
public sealed class EstimationScaleNavigationDto : IMapFrom<EstimationScale>
{
    public Guid Id { get; set; }
    public int Key { get; set; }
    public required string Name { get; set; }
}
