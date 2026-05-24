using Wayd.Common.Application.Enums;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Domain.Models;

public enum SyncRunStatus
{
    Running = 0,
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3
}

public enum SyncTriggerSource
{
    Scheduled = 0,
    Manual = 1,
    Api = 2
}

/// <summary>
/// Persisted record of a single sync run for one connection. Inserted at start with
/// <see cref="SyncRunStatus.Running"/>; updated on completion (or failure / cancellation).
/// History survives connection deletion — there is intentionally no FK to Connections.
/// </summary>
public sealed class SyncRun : BaseEntity
{
    private SyncRun() { }

    public Guid ConnectionId { get; private set; }
    public Connector ConnectorType { get; private set; }
    public Instant StartedAt { get; private set; }
    public Instant? FinishedAt { get; private set; }
    public SyncRunStatus Status { get; private set; }
    public SyncTriggerSource TriggerSource { get; private set; }
    public SyncType SyncType { get; private set; }
    public int WorkspacesPlanned { get; private set; }
    public int WorkspacesSucceeded { get; private set; }
    public int WorkspacesFailed { get; private set; }
    public int WorkItemsProcessed { get; private set; }
    public int ErrorsCount { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? DetailsJson { get; private set; }

    public void SetWorkspacesPlanned(int count) => WorkspacesPlanned = count;

    public void RecordWorkspaceSuccess(int workItemsProcessed)
    {
        WorkspacesSucceeded++;
        WorkItemsProcessed += workItemsProcessed;
    }

    public void RecordWorkspaceFailure()
    {
        WorkspacesFailed++;
        ErrorsCount++;
    }

    public void RecordError() => ErrorsCount++;

    public Result MarkSucceeded(Instant now)
    {
        if (Status != SyncRunStatus.Running)
            return Result.Failure($"Cannot mark a {Status} sync run as Succeeded.");
        Status = SyncRunStatus.Succeeded;
        FinishedAt = now;
        return Result.Success();
    }

    public Result MarkFailed(Instant now, string error)
    {
        if (Status != SyncRunStatus.Running)
            return Result.Failure($"Cannot mark a {Status} sync run as Failed.");
        Status = SyncRunStatus.Failed;
        FinishedAt = now;
        ErrorMessage = error;
        ErrorsCount++;
        return Result.Success();
    }

    public Result MarkCancelled(Instant now)
    {
        if (Status != SyncRunStatus.Running)
            return Result.Failure($"Cannot mark a {Status} sync run as Cancelled.");
        Status = SyncRunStatus.Cancelled;
        FinishedAt = now;
        return Result.Success();
    }

    public void SetDetails(string json) => DetailsJson = json;

    public static SyncRun Start(Guid connectionId, Connector connector, SyncType syncType, SyncTriggerSource trigger, Instant now)
    {
        return new SyncRun
        {
            ConnectionId = connectionId,
            ConnectorType = connector,
            SyncType = syncType,
            TriggerSource = trigger,
            StartedAt = now,
            Status = SyncRunStatus.Running
        };
    }
}
