namespace Wayd.Common.Application.Requests.WorkManagement.Commands;

/// <summary>
/// Deletes a workspace's work item history and clears its watermark, so the next history sync
/// replays the source's revisions from the start. A full sync runs it first.
/// </summary>
/// <param name="WorkspaceId">The workspace whose history is rebuilt.</param>
public sealed record ResetWorkItemHistoryCommand(Guid WorkspaceId) : ICommand, ILongRunningRequest;
