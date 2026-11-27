using Wayd.AppIntegration.Domain.Interfaces;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.AppIntegration.Domain.Models.Workday;

namespace Wayd.AppIntegration.Application.Connections.ExternalSystems;

/// <inheritdoc cref="IExternalSystemLinker"/>
public sealed class ExternalSystemLinker(IAppIntegrationDbContext appIntegrationDbContext, IAzureDevOpsUrlResolver azureDevOpsUrlResolver)
    : IExternalSystemLinker
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly IAzureDevOpsUrlResolver _azureDevOpsUrlResolver = azureDevOpsUrlResolver;

    // Systems created in this scope and not yet saved, which a query cannot find: two connections to one new
    // system linked before one save must share a row, or the save breaks the unique index.
    private readonly List<ExternalSystem> _created = [];

    public async Task<Result> Link(Connection connection, EventActor actor, Instant timestamp, CancellationToken cancellationToken)
    {
        if (connection is not ISyncableConnection syncable || string.IsNullOrWhiteSpace(syncable.SystemId))
            return Result.Success();

        var (name, baseUrl) = Describe(connection);

        var system = await Find(connection, syncable.SystemId, cancellationToken);
        if (system is null)
        {
            system = ExternalSystem.Create(connection.Connector, syncable.SystemId, name, baseUrl, actor, timestamp);
            await _appIntegrationDbContext.ExternalSystems.AddAsync(system, cancellationToken);
            _created.Add(system);
        }
        else
        {
            system.UpdateDetails(name, baseUrl, actor, timestamp);
        }

        return connection.LinkExternalSystem(system, actor, timestamp);
    }

    private async Task<ExternalSystem?> Find(Connection connection, string systemId, CancellationToken cancellationToken)
    {
        if (connection.ExternalSystemId is { } linkedId)
        {
            return await _appIntegrationDbContext.ExternalSystems
                .FirstOrDefaultAsync(s => s.Id == linkedId, cancellationToken);
        }

        var created = _created.FirstOrDefault(s => s.Is(connection.Connector, systemId));
        if (created is not null)
            return created;

        var trimmed = systemId.Trim();
        return await _appIntegrationDbContext.ExternalSystems
            .FirstOrDefaultAsync(s => s.Connector == connection.Connector && s.SystemId == trimmed, cancellationToken);
    }

    /// <summary>The name and base URL a connection gives the system it reaches.</summary>
    private (string Name, string? BaseUrl) Describe(Connection connection) => connection switch
    {
        AzureDevOpsBoardsConnection azdo => (
            azdo.Configuration.Organization,
            _azureDevOpsUrlResolver.GetOrganizationUrl(azdo.Configuration.Organization)),
        EntraConnection entra => (entra.Configuration.TenantId, null),
        WorkdayConnection workday => (workday.Configuration.TenantAlias, $"https://{workday.Configuration.ServiceHost}"),
        _ => throw new InvalidOperationException(
            $"No external system description is registered for connection type '{connection.GetType().Name}'."),
    };
}
