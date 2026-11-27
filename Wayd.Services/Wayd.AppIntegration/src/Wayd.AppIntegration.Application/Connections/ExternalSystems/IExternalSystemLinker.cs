namespace Wayd.AppIntegration.Application.Connections.ExternalSystems;

/// <summary>
/// Ties a syncable connection to the <see cref="ExternalSystem"/> its system id names, finding or creating that
/// system, and refreshes the system's name and base URL from the connection. Changes are left for the caller to
/// save.
/// </summary>
public interface IExternalSystemLinker : IScopedService
{
    /// <summary>
    /// Links <paramref name="connection"/> once it knows its system id and refreshes the system's details from it.
    /// Does nothing for a connection that syncs nothing or does not yet know its system id.
    /// </summary>
    Task<Result> Link(Connection connection, EventActor actor, Instant timestamp, CancellationToken cancellationToken);
}
