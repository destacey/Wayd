namespace Wayd.Integrations.Abstractions;

/// <summary>
/// Builds a <see cref="SyncableConnectionDescriptor"/> for a connection — i.e. loads the typed
/// EF entity for a connector and boxes its configuration into the connector-neutral descriptor
/// the runner hands to <see cref="IWorkItemSourceFactory"/>.
///
/// Implementations are registered keyed by <see cref="Connector"/>. The runner resolves the
/// right builder for each connection's connector value, so adding a new connector means adding
/// one builder rather than editing the runner.
/// </summary>
public interface ISyncableConnectionDescriptorBuilder
{
    Connector Connector { get; }

    Task<Result<SyncableConnectionDescriptor>> Build(Guid connectionId, CancellationToken cancellationToken);
}
