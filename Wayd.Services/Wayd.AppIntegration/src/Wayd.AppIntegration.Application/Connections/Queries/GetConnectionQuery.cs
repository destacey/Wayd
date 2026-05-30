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

internal sealed class GetConnectionQueryHandler(IAppIntegrationDbContext appIntegrationDbContext) : IQueryHandler<GetConnectionQuery, ConnectionDetailsDto?>
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
        // configuration block and the $type discriminator both survive serialization. Missing an arm
        // here is silent: the falls-through default emits the base DTO with no Configuration, which
        // looks to the UI like every field is null.
        return connection switch
        {
            AzureDevOpsBoardsConnection => connection.Adapt<AzureDevOpsConnectionDetailsDto>(),
            AzureOpenAIConnection => connection.Adapt<AzureOpenAIConnectionDetailsDto>(),
            EntraConnection => connection.Adapt<EntraConnectionDetailsDto>(),
            WorkdayConnection => connection.Adapt<WorkdayConnectionDetailsDto>(),
            // case OpenAIConnection:
            _ => connection.Adapt<ConnectionDetailsDto>(),
        };
    }
}
