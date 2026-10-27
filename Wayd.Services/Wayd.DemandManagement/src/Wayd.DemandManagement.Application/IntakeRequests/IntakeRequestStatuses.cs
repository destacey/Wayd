using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.DemandManagement.Domain;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests;

/// <summary>
/// Reads the review workflow governing a desk, and the statuses a request moved there can arrive in.
/// </summary>
/// <remarks>
/// The desk is the assignment scope: a desk with a workflow of its own follows it, and every other desk the
/// organization's default. Shared by the commands that move a request and the queries that offer the moves,
/// so a form never offers what the command refuses.
/// </remarks>
internal static class IntakeRequestStatuses
{
    /// <summary>
    /// The failure for a request whose desk now follows a different workflow from the one the request is on,
    /// which only a reassignment that has not moved its requests could leave.
    /// </summary>
    public const string WorkflowMismatchError =
        "This request is on a different workflow from its desk's, so its status cannot change until it is moved onto the desk's workflow.";

    /// <summary>The workflow governing the desk, with its statuses.</summary>
    public static Task<Result<StatusWorkflow>> ForDesk(this IStatusResolver resolver, Guid deskId, CancellationToken cancellationToken) =>
        resolver.ForScope(DemandManagementWorkflowOwners.IntakeRequest.Key, deskId, cancellationToken);

    /// <summary>
    /// The statuses of <paramref name="destination"/> a request can arrive in when moved to its desk, in the
    /// workflow's order, and the one it arrives in unless the mover chooses: the status it holds, when the
    /// desk shares its workflow, otherwise the one with the same meaning or, failing that, the same name.
    /// </summary>
    /// <returns>The arrival statuses, and the default among them, or null when none fits.</returns>
    public static (IReadOnlyList<WorkflowStatus> Statuses, WorkflowStatus? Default) ArrivalStatuses(
        StatusWorkflow destination, IntakeRequest request)
    {
        var statuses = destination.Statuses
            .Where(s => IntakeRequest.CanArriveIn(s.Category))
            .OrderBy(s => s.Order)
            .ToList();

        WorkflowStatus? fallback = destination.Id == request.StatusWorkflowId
            ? statuses.FirstOrDefault(s => s.Id == request.StatusId)
            : (request.StatusAlias == IntakeRequestStatusAlias.None
                    ? null
                    : statuses.FirstOrDefault(s => s.Alias == (int)request.StatusAlias))
                ?? statuses.FirstOrDefault(s => string.Equals(s.Name, request.StatusName, StringComparison.OrdinalIgnoreCase));

        return (statuses, fallback);
    }
}
