using Mapster;
using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureOpenAI;
using Wayd.AppIntegration.Application.Connections.Dtos.Entra;
using Wayd.AppIntegration.Application.Connections.Dtos.Workday;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.AppIntegration.Domain.Models.Workday;

namespace Wayd.AppIntegration.Application.Connections.Queries;

public sealed record GetConnectionQuery(Guid Id) : IQuery<ConnectionDetailsDto?>;

public sealed class GetConnectionQueryHandler(IAppIntegrationDbContext appIntegrationDbContext) : IQueryHandler<GetConnectionQuery, ConnectionDetailsDto?>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;

    public async Task<ConnectionDetailsDto?> Handle(GetConnectionQuery request, CancellationToken cancellationToken)
    {
        var connection = await _appIntegrationDbContext.Connections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, cancellationToken);

        if (connection == null)
        {
            return null;
        }

        // Polymorphic adapt — each arm projects the connection-typed row to its concrete DTO so the
        // configuration block and the $type discriminator both survive serialization. Every concrete
        // connection type must have an arm here — falling through to the base DTO would silently drop
        // the configuration, so throw instead.
        //
        // Secrets are masked here rather than at the call site, so a new caller cannot leak one by
        // forgetting to. Callers needing the real credential use the connector-specific queries.
        switch (connection)
        {
            case AzureDevOpsBoardsConnection:
                var azdo = connection.Adapt<AzureDevOpsConnectionDetailsDto>();
                azdo.Configuration.MaskPersonalAccessToken();
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

            default:
                throw new InvalidOperationException(
                    $"No details DTO mapping is registered for connection type '{connection.GetType().Name}'.");
        }
    }
}
