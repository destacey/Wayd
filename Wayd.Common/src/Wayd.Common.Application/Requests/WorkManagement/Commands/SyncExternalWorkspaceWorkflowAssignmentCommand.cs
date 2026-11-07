namespace Wayd.Common.Application.Requests.WorkManagement.Commands;

/// <summary>
/// Points an externally managed workspace at the managed workflow scheme of its current work process, as
/// after its project moves to another process.
/// </summary>
/// <param name="WorkspaceId">The internal Wayd workspace id.</param>
public sealed record SyncExternalWorkspaceWorkflowAssignmentCommand(Guid WorkspaceId) : ICommand;
