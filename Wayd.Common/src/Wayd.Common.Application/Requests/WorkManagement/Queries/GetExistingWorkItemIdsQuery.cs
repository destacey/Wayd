namespace Wayd.Common.Application.Requests.WorkManagement.Queries;

/// <summary>
/// Which of the given work items exist in Work. Asked by another module that points at work items by id and
/// keeps no copy of them, such as Demand Management's outcome import.
/// </summary>
/// <param name="Ids">The work items to look for.</param>
public sealed record GetExistingWorkItemIdsQuery(IReadOnlyCollection<Guid> Ids) : IQuery<IReadOnlySet<Guid>>;
