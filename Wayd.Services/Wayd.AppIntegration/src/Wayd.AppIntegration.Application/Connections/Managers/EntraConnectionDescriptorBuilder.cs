using Wayd.AppIntegration.Domain.Interfaces;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Abstractions;

namespace Wayd.AppIntegration.Application.Connections.Managers;

/// <summary>
/// Builds a <see cref="SyncableConnectionDescriptor"/> for an <c>EntraConnection</c> by loading
/// the EF entity and boxing its configuration. Registered keyed by <see cref="Connector.Entra"/>.
/// </summary>
public sealed class EntraConnectionDescriptorBuilder(IAppIntegrationDbContext db) : ISyncableConnectionDescriptorBuilder
{
    private readonly IAppIntegrationDbContext _db = db;

    public Connector Connector => Connector.Entra;

    public async Task<Result<SyncableConnectionDescriptor>> Build(Guid connectionId, CancellationToken cancellationToken)
    {
        var entity = await _db.EntraConnections.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);
        if (entity is null)
            return Result.Failure<SyncableConnectionDescriptor>($"Entra connection {connectionId} not found.");

        return Result.Success(new SyncableConnectionDescriptor(
            ConnectionId: entity.Id,
            Connector: Connector.Entra,
            SystemId: ((ISyncableConnection)entity).SystemId,
            Configuration: entity.Configuration,
            TeamConfiguration: null));
    }
}
