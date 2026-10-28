namespace Wayd.ProductManagement.Application.IncidentSeveritySets;

/// <summary>
/// Returns severity level ids still referenced by incidents.
/// </summary>
public interface IIncidentSeverityLevelUsageQuery
{
    Task<IReadOnlySet<Guid>> GetLevelIdsInUse(CancellationToken cancellationToken);
}
