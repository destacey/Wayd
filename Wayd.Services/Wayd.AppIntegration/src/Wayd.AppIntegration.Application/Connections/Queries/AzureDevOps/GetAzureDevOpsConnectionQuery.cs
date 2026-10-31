using Mapster;
using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;

namespace Wayd.AppIntegration.Application.Connections.Queries.AzureDevOps;

public sealed record GetAzureDevOpsConnectionQuery(Guid ConnectionId) : IQuery<AzureDevOpsConnectionDetailsDto?>;

public sealed class GetAzureDevOpsConnectionQueryHandler(IAppIntegrationDbContext appIntegrationDbContext, IAzureDevOpsUrlResolver urlResolver) : IQueryHandler<GetAzureDevOpsConnectionQuery, AzureDevOpsConnectionDetailsDto?>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly IAzureDevOpsUrlResolver _urlResolver = urlResolver;

    public async Task<AzureDevOpsConnectionDetailsDto?> Handle(GetAzureDevOpsConnectionQuery request, CancellationToken cancellationToken)
    {
        var connection = await _appIntegrationDbContext.AzureDevOpsBoardsConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ConnectionId, cancellationToken);
        if (connection is null)
            return null;

        var dto = connection.Adapt<AzureDevOpsConnectionDetailsDto>();
        dto.Configuration.ResolveOrganizationUrl(_urlResolver);
        return dto;

        // TODO: using ProjectTo is not working with the JSON Column - .ProjectToType<AzureDevOpsConnectionDetailsDto>()
    }
}
