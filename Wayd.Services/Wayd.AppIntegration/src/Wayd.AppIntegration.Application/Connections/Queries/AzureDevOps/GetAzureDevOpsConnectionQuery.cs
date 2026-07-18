using Mapster;
using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;

namespace Wayd.AppIntegration.Application.Connections.Queries.AzureDevOps;

public sealed record GetAzureDevOpsConnectionQuery(Guid ConnectionId) : IQuery<AzureDevOpsConnectionDetailsDto?>;

public sealed class GetAzureDevOpsConnectionQueryHandler(IAppIntegrationDbContext appIntegrationDbContext) : IQueryHandler<GetAzureDevOpsConnectionQuery, AzureDevOpsConnectionDetailsDto?>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;

    public async Task<AzureDevOpsConnectionDetailsDto?> Handle(GetAzureDevOpsConnectionQuery request, CancellationToken cancellationToken)
    {
        var connection = await _appIntegrationDbContext.AzureDevOpsBoardsConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ConnectionId, cancellationToken);

        return connection?.Adapt<AzureDevOpsConnectionDetailsDto>();

        // TODO: using ProjectTo is not working with the JSON Column - .ProjectToType<AzureDevOpsConnectionDetailsDto>()
    }
}
