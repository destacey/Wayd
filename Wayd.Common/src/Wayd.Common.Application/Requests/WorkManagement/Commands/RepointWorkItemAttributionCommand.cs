namespace Wayd.Common.Application.Requests.WorkManagement.Commands;

/// <summary>
/// Repoints work items attributed to one external identity at a Wayd employee, or clears them.
/// Raised when an admin maps or ignores an identity, so the decision reaches work already synced
/// rather than only future syncs.
/// </summary>
/// <param name="ExternalId">The external system's identity id, as stored on WorkItemsExtended.</param>
/// <param name="EmployeeId">
/// The employee to attribute the work to, or null to clear the attribution — which is what an
/// admin means by ignoring an identity that had been matched to the wrong person.
/// </param>
public sealed record RepointWorkItemAttributionCommand(string ExternalId, Guid? EmployeeId)
    : ICommand, ILongRunningRequest;
