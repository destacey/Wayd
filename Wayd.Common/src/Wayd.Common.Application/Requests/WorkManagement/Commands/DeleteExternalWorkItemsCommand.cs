namespace Wayd.Common.Application.Requests.WorkManagement.Commands;

/// <summary>
/// Deletes the external work items with the given ids from whichever of <paramref name="WorkspaceIds"/>
/// holds them.
/// </summary>
/// <param name="WorkspaceIds">
/// Every workspace the connection syncs. An item can still sit in the workspace of a project it has
/// left, so deleting from only the workspace whose sync found it would leave it behind; an external
/// id identifies one item across a connection's workspaces.
/// </param>
/// <param name="WorkItemIds">The external ids of the items to delete.</param>
public sealed record DeleteExternalWorkItemsCommand(IReadOnlyCollection<Guid> WorkspaceIds, int[] WorkItemIds) : ICommand, ILongRunningRequest;
