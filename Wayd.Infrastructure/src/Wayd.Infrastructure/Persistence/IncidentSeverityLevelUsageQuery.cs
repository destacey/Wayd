using Wayd.ProductManagement.Application.IncidentSeveritySets;

namespace Wayd.Infrastructure.Persistence;

/// <summary>
/// Returns no levels in use until incident records exist.
/// </summary>
public sealed class IncidentSeverityLevelUsageQuery : IIncidentSeverityLevelUsageQuery
{
    public Task<IReadOnlySet<Guid>> GetLevelIdsInUse(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
}
