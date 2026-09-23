using Wayd.Common.Application.Dtos;

namespace Wayd.Work.Application.WorkItems.Dtos;

public sealed record ForecastDependencyLinkDto
{
    public required ForecastWorkItemDto Predecessor { get; init; }
    public required ForecastWorkItemDto Successor { get; init; }

    /// <summary>
    /// Why the forecast left the dependency out: an <see cref="IgnoredDependencyReason"/>.
    /// </summary>
    public required SimpleNavigationDto Reason { get; init; }
}
