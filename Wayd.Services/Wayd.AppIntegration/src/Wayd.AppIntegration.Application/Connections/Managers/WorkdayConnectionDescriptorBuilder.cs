using Wayd.AppIntegration.Domain.Interfaces;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Abstractions;

namespace Wayd.AppIntegration.Application.Connections.Managers;

/// <summary>
/// Builds a <see cref="SyncableConnectionDescriptor"/> for a <c>WorkdayConnection</c> by loading
/// the EF entity and boxing its configuration. Registered keyed by <see cref="Connector.Workday"/>.
/// </summary>
public sealed class WorkdayConnectionDescriptorBuilder(IAppIntegrationDbContext db) : ISyncableConnectionDescriptorBuilder
{
    private readonly IAppIntegrationDbContext _db = db;

    public Connector Connector => Connector.Workday;

    public async Task<Result<SyncableConnectionDescriptor>> Build(Guid connectionId, CancellationToken cancellationToken)
    {
        var entity = await _db.WorkdayConnections.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);
        if (entity is null)
            return Result.Failure<SyncableConnectionDescriptor>($"Workday connection {connectionId} not found.");

        return Result.Success(new SyncableConnectionDescriptor(
            ConnectionId: entity.Id,
            Connector: Connector.Workday,
            SystemId: ((ISyncableConnection)entity).SystemId,
            Configuration: entity.Configuration,
            TeamConfiguration: null));
    }
}
