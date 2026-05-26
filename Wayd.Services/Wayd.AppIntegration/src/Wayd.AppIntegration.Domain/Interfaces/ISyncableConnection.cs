namespace Wayd.AppIntegration.Domain.Interfaces;

/// <summary>
/// Marker interface for connections that synchronize per-target state with an external system
/// (e.g. workspaces, processes). Connections without per-target state — like a People Sync
/// connector that just fetches users — don't need to implement this; the runner falls back to
/// <c>IsActive &amp;&amp; IsValidConfiguration</c>.
/// </summary>
public interface ISyncableConnection
{
    /// <summary>
    /// The unique identifier for the external system this connection syncs with.
    /// </summary>
    string? SystemId { get; }

    /// <summary>
    /// Computed property indicating whether the connection can currently sync.
    /// Requires: IsActive &amp;&amp; IsValidConfiguration &amp;&amp; HasActiveIntegrationObjects.
    /// </summary>
    bool CanSync { get; }
}
