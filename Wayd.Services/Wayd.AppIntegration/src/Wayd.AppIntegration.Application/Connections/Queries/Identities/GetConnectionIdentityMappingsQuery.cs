using Mapster;
using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Application.Connections.Dtos.Identities;
using Wayd.Common.Domain.AppIntegrations;
using Wayd.Common.Domain.Employees;

namespace Wayd.AppIntegration.Application.Connections.Queries.Identities;

/// <summary>
/// Every external identity a connection's syncs have seen, for the People tab.
/// </summary>
/// <param name="UnmappedOnly">Restricts to identities still awaiting an admin decision.</param>
public sealed record GetConnectionIdentityMappingsQuery(Guid ConnectionId, bool UnmappedOnly = false)
    : IQuery<List<ExternalIdentityMappingDto>>;

public sealed class GetConnectionIdentityMappingsQueryHandler(IAppIntegrationDbContext appIntegrationDbContext)
    : IQueryHandler<GetConnectionIdentityMappingsQuery, List<ExternalIdentityMappingDto>>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;

    public async Task<List<ExternalIdentityMappingDto>> Handle(GetConnectionIdentityMappingsQuery request, CancellationToken cancellationToken)
    {
        var query = _appIntegrationDbContext.ExternalIdentityMappings
            .AsNoTracking()
            .Include(m => m.Employee)
            .Where(m => m.ConnectionId == request.ConnectionId);

        if (request.UnmappedOnly)
        {
            query = query.Where(m => m.Status == ExternalIdentityMappingStatus.Unmapped);
        }

        return await query
            // Unmapped first: the queue exists to be emptied, so what needs a decision leads.
            // Within a status, most recently active first — those are the people who matter now.
            .OrderBy(m => m.Status == ExternalIdentityMappingStatus.Unmapped ? 0 : 1)
            .ThenByDescending(m => m.LastSeen)
            .ProjectToType<ExternalIdentityMappingDto>()
            .ToListAsync(cancellationToken);
    }
}
