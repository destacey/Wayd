namespace Wayd.Common.Application.Logging;

/// <summary>
/// Centralized application EventIds to avoid duplicates across the solution.
/// Reserve ranges per subsystem if the project grows.
/// </summary>
public enum AppEventId
{
    // Service Projects
    // Azure DevOps integration (10000-10999)
    AppIntegration_ExternalCallElapsed = 10000,
    AppIntegration_CancellationRequested = 10001,
    AppIntegration_AzureDevOpsBoardsSyncManager_SyncStarted = 10100,
    AppIntegration_AzureDevOpsBoardsSyncManager_WorkProcessSynced = 10101,
    AppIntegration_AzureDevOpsBoardsSyncManager_WorkspaceSynced = 10102,
    AppIntegration_AzureDevOpsBoardsSyncManager_WorkspaceWorkItemsSynced = 10103,
    AppIntegration_AzureDevOpsBoardsSyncManager_NoActiveWorkProcesses = 10104,
    AppIntegration_AzureDevOpsBoardsSyncManager_NoActiveWorkspaces = 10105,
    AppIntegration_AzureDevOpsBoardsSyncManager_WorkItemsRetrieved = 10106,
    AppIntegration_AzureDevOpsBoardsSyncManager_ParentChangesRetrieved = 10107,
    AppIntegration_AzureDevOpsBoardsSyncManager_DependencyChangesRetrieved = 10108,
    AppIntegration_AzureDevOpsBoardsSyncManager_DeletedWorkItemsRetrieved = 10109,
    AppIntegration_AzureDevOpsBoardsSyncManager_SyncSummary = 10110,

    // WorkSyncRunner (generic orchestrator across all WorkSync-category connectors)
    AppIntegration_WorkSyncRunner_RunStarted = 10200,
    AppIntegration_WorkSyncRunner_RunSummary = 10201,

    // Integration Projects
    // Integrations.AzureDevOps (100000-100999)
    Integrations_AzureDevOps_ProjectService_DuplicateIterationTeamMapping = 100100,

}

public static class AppEventIdExtensions
{
    public static EventId ToEventId(this AppEventId appEventId)
    {
        return new EventId((int)appEventId, appEventId.ToString());
    }
}