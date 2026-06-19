using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Domain.Interfaces;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Abstractions;

namespace Wayd.AppIntegration.Application.Connections.Managers;

/// <summary>
/// Builds a <see cref="SyncableConnectionDescriptor"/> for an
/// <c>AzureDevOpsBoardsConnection</c> by loading the EF entity and boxing its configuration
/// and team configuration. Registered keyed by <see cref="Connector.AzureDevOps"/>.
/// </summary>
public sealed class AzureDevOpsConnectionDescriptorBuilder(IAppIntegrationDbContext db) : ISyncableConnectionDescriptorBuilder
{
    private readonly IAppIntegrationDbContext _db = db;

    public Connector Connector => Connector.AzureDevOps;

    public async Task<Result<SyncableConnectionDescriptor>> Build(Guid connectionId, CancellationToken cancellationToken)
    {
        var entity = await _db.AzureDevOpsBoardsConnections.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);
        if (entity is null)
            return Result.Failure<SyncableConnectionDescriptor>($"Azure DevOps connection {connectionId} not found.");

        return Result.Success(new SyncableConnectionDescriptor(
            ConnectionId: entity.Id,
            Connector: Connector.AzureDevOps,
            SystemId: ((ISyncableConnection)entity).SystemId,
            Configuration: entity.Configuration,
            TeamConfiguration: entity.TeamConfiguration));
    }
}
