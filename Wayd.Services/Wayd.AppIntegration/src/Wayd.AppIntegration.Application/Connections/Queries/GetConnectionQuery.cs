using Mapster;
using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureOpenAI;
using Wayd.AppIntegration.Application.Connections.Dtos.Entra;
using Wayd.AppIntegration.Application.Connections.Dtos.Workday;
using Wayd.AppIntegration.Application.Connections.Dtos.Teams;
using Wayd.AppIntegration.Domain.Models.Teams;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.AppIntegration.Domain.Models.Workday;

namespace Wayd.AppIntegration.Application.Connections.Queries;

public sealed record GetConnectionQuery(Guid Id) : IQuery<ConnectionDetailsDto?>;

public sealed class GetConnectionQueryHandler(IAppIntegrationDbContext appIntegrationDbContext, IAzureDevOpsUrlResolver azureDevOpsUrlResolver) : IQueryHandler<GetConnectionQuery, ConnectionDetailsDto?>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly IAzureDevOpsUrlResolver _azureDevOpsUrlResolver = azureDevOpsUrlResolver;

    public async Task<ConnectionDetailsDto?> Handle(GetConnectionQuery request, CancellationToken cancellationToken)
    {
        var connection = await _appIntegrationDbContext.Connections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, cancellationToken);

        if (connection == null)
        {
            return null;
        }

        var dto = Describe(connection);
        dto.ExternalSystem = connection.ExternalSystemId is { } systemId
            ? await _appIntegrationDbContext.ExternalSystems
                .Where(s => s.Id == systemId)
                .Select(s => new ExternalSystemNavigationDto { Id = s.Id, Name = s.Name, BaseUrl = s.BaseUrl })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return dto;
    }

    /// <summary>
    /// Maps the connection to its concrete details DTO, so the configuration block and the $type discriminator
    /// both survive serialization, and masks its secrets here rather than at the call site so a new caller cannot
    /// leak one. A connection type with no arm throws rather than falling back to the base DTO, which would drop
    /// the configuration. Callers needing the real credential use the connector-specific queries.
    /// </summary>
    private ConnectionDetailsDto Describe(Connection connection)
    {
        switch (connection)
        {
            case AzureDevOpsBoardsConnection:
                var azdo = connection.Adapt<AzureDevOpsConnectionDetailsDto>();
                azdo.Configuration.MaskPersonalAccessToken();
                azdo.Configuration.ResolveOrganizationUrl(_azureDevOpsUrlResolver);
                return azdo;

            case AzureOpenAIConnection:
                var aoai = connection.Adapt<AzureOpenAIConnectionDetailsDto>();
                aoai.Configuration.MaskApiKey();
                return aoai;

            case EntraConnection:
                var entra = connection.Adapt<EntraConnectionDetailsDto>();
                entra.Configuration.MaskClientSecret();
                return entra;

            case WorkdayConnection:
                var workday = connection.Adapt<WorkdayConnectionDetailsDto>();
                workday.Configuration.MaskIsuPassword();
                return workday;

            case TeamsConnection:
                var teams = connection.Adapt<TeamsConnectionDetailsDto>();
                teams.Configuration.MaskClientSecret();
                return teams;

            default:
                throw new InvalidOperationException(
                    $"No details DTO mapping is registered for connection type '{connection.GetType().Name}'.");
        }
    }
}
