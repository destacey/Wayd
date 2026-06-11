using Mapster;
using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureOpenAI;
using Wayd.AppIntegration.Application.Connections.Dtos.Entra;
using Wayd.AppIntegration.Application.Connections.Dtos.Workday;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.AppIntegration.Domain.Models.Workday;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Connections.Queries;

public sealed record GetConnectionsQuery(
    bool IncludeInactive = false,
    Connector? Type = null,
    ConnectorCapability? Capability = null) : IQuery<IReadOnlyList<ConnectionListDto>>;

internal sealed class GetConnectionsQueryHandler(IAppIntegrationDbContext appIntegrationDbContext) : IQueryHandler<GetConnectionsQuery, IReadOnlyList<ConnectionListDto>>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;

    public async Task<IReadOnlyList<ConnectionListDto>> Handle(GetConnectionsQuery request, CancellationToken cancellationToken)
    {
        var query = _appIntegrationDbContext.Connections.AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(c => c.IsActive);

        if (request.Type.HasValue)
            query = query.Where(c => c.Connector == request.Type.Value);

        // Load to memory first to avoid Mapster projection issues with ISyncableConnection interface
        var connections = await query.ToListAsync(cancellationToken);

        // Capability filter is evaluated in-memory because it depends on the connector-capability
        // lookup, which isn't translatable to SQL. The connection set is small (<1000 rows) so this is fine.
        if (request.Capability.HasValue)
            connections = [.. connections.Where(c => c.Connector.HasCapability(request.Capability.Value))];

        // Map each connection to its appropriate derived DTO type for polymorphic serialization.
        // Every concrete connection type must have an arm here — falling through to the base DTO
        // would silently drop the configuration and $type discriminator, so throw instead.
        return [.. connections.Select(connection => connection switch
        {
            AzureDevOpsBoardsConnection azdo => (ConnectionListDto)azdo.Adapt<AzureDevOpsConnectionListDto>(),
            AzureOpenAIConnection aoai => aoai.Adapt<AzureOpenAIConnectionListDto>(),
            EntraConnection entra => entra.Adapt<EntraConnectionListDto>(),
            WorkdayConnection workday => workday.Adapt<WorkdayConnectionListDto>(),
            _ => throw new InvalidOperationException(
                $"No list DTO mapping is registered for connection type '{connection.GetType().Name}'.")
        })];
    }
}
