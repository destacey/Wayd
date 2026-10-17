using Wayd.Common.Application.Dtos;

namespace Wayd.Work.Application.WorkItems.Dtos;

public sealed record ForecastDependencyLinkDto
{
    public required ForecastWorkItemDto Predecessor { get; init; }
    public required ForecastWorkItemDto Successor { get; init; }

    /// <summary>
    /// Why the forecast left the dependency out: Closes a Cycle or Predecessor Removed.
    /// </summary>
    public required SimpleNavigationDto Reason { get; init; }
}
