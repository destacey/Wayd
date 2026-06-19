namespace Wayd.AppIntegration.Domain.Interfaces;

/// <summary>
/// Implemented by every connection that participates in a sync runner — work-sync (AzDO),
/// people-sync (Entra), and future runner-driven categories. Lets both runners share one
/// readiness predicate (<see cref="CanSync"/>) instead of inlining their own filters.
/// AI-provider connectors don't implement this — they aren't sync-runner-driven.
/// </summary>
public interface ISyncableConnection
{
    /// <summary>
    /// The unique identifier for the external system this connection syncs with. Nullable —
    /// connectors that don't have an AzDO-style external system id (e.g. Entra, where the
    /// tenant id lives in <c>Configuration</c>) return <c>null</c>.
    /// </summary>
    string? SystemId { get; }

    /// <summary>
    /// Computed property indicating whether the connection can currently sync.
    /// Requires: IsActive &amp;&amp; IsValidConfiguration &amp;&amp; HasActiveIntegrationObjects.
    /// </summary>
    bool CanSync { get; }
}
