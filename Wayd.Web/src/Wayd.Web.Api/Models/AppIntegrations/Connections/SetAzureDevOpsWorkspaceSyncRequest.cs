namespace Wayd.Web.Api.Models.AppIntegrations.Connections;

/// <summary>
/// Turns syncing on or off for an Azure DevOps project that has been set up on the connection.
/// The project's Wayd workspace and work items are kept either way.
/// </summary>
public sealed record SetAzureDevOpsWorkspaceSyncRequest
{
    /// <summary>
    /// Whether the project's work items are synced.
    /// </summary>
    public bool IsEnabled { get; set; }
}
