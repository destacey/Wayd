using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Requests.AppIntegration;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Connections.Queries;

public sealed class GetActiveCommunicationConnectionsQueryHandler(IAppIntegrationDbContext appIntegrationDbContext)
    : IQueryHandler<GetActiveCommunicationConnectionsQuery, IReadOnlyList<ActiveCommunicationConnectionSummaryDto>>
{
    private static readonly HashSet<Connector> CommunicationConnectors =
        Enum.GetValues<Connector>().Where(c => c.HasCapability(ConnectorCapability.Communication)).ToHashSet();

    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;

    public async Task<IReadOnlyList<ActiveCommunicationConnectionSummaryDto>> Handle(
        GetActiveCommunicationConnectionsQuery request,
        CancellationToken cancellationToken)
    {
        return await _appIntegrationDbContext.Connections
            .AsNoTracking()
            .Where(c => c.IsActive && c.IsValidConfiguration && !c.IsDeleted && CommunicationConnectors.Contains(c.Connector))
            .OrderBy(c => c.Name)
            .Select(c => new ActiveCommunicationConnectionSummaryDto
            {
                Id = c.Id,
                Name = c.Name,
                Connector = c.Connector,
                IsActive = c.IsActive,
                IsValidConfiguration = c.IsValidConfiguration,
            })
            .ToListAsync(cancellationToken);
    }
}
