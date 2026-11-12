using Microsoft.EntityFrameworkCore;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Application.IncidentSeveritySets;

namespace Wayd.Infrastructure.Persistence;

/// <summary>
/// Returns severity level ids referenced by incidents.
/// </summary>
public sealed class IncidentSeverityLevelUsageQuery(IProductManagementDbContext dbContext)
    : IIncidentSeverityLevelUsageQuery
{
    private readonly IProductManagementDbContext _dbContext = dbContext;

    public async Task<IReadOnlySet<Guid>> GetLevelIdsInUse(CancellationToken cancellationToken)
    {
        var ids = await _dbContext.Incidents
            .Select(i => i.SeverityLevelId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }
}
