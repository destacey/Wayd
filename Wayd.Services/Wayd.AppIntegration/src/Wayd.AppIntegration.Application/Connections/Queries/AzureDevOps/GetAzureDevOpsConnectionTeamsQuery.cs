using Mapster;
using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;

namespace Wayd.AppIntegration.Application.Connections.Queries.AzureDevOps;

public sealed record GetAzureDevOpsConnectionTeamsQuery(Guid ConnectionId, Guid? WorkspaceId = null) : IQuery<List<AzureDevOpsWorkspaceTeamDto>>;

public sealed class GetAzureDevOpsConnectionTeamsQueryHandler(IAppIntegrationDbContext appIntegrationDbContext) : IQueryHandler<GetAzureDevOpsConnectionTeamsQuery, List<AzureDevOpsWorkspaceTeamDto>>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;

    public async Task<List<AzureDevOpsWorkspaceTeamDto>> Handle(GetAzureDevOpsConnectionTeamsQuery request, CancellationToken cancellationToken)
    {
        var query = _appIntegrationDbContext.AzureDevOpsBoardsConnections
            .Where(c => c.Id == request.ConnectionId)
            .SelectMany(c => c.TeamConfiguration.WorkspaceTeams);

        if (request.WorkspaceId.HasValue)
        {
            query = query.Where(t => t.WorkspaceId == request.WorkspaceId);
        }

        var teams = await query
            .ProjectToType<AzureDevOpsWorkspaceTeamDto>()
            .ToListAsync(cancellationToken);

        return teams;
    }
}
