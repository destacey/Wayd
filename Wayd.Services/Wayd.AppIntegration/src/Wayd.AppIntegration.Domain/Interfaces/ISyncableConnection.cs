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
    /// The outside system's own identifier for the instance this connection syncs with: the Azure DevOps
    /// organization, the Entra tenant, the Workday tenant. Null until an Azure DevOps connection has
    /// read its organization's id.
    /// </summary>
    string? SystemId { get; }

    /// <summary>
    /// Computed property indicating whether the connection can currently sync.
    /// Requires: IsActive &amp;&amp; IsValidConfiguration &amp;&amp; HasActiveIntegrationObjects.
    /// </summary>
    bool CanSync { get; }
}
