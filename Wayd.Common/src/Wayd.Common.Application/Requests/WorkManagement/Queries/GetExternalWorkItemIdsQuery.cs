namespace Wayd.Common.Application.Requests.WorkManagement.Queries;

/// <summary>The external ids of the work items held in the given workspaces.</summary>
public sealed record GetExternalWorkItemIdsQuery(IReadOnlyCollection<Guid> WorkspaceIds) : IQuery<Result<int[]>>;
