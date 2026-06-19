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