using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.ProductManagement.Application.DeliveryWorkflows;

/// <summary>
/// Answers which lifecycle actions a delivery record's workflow allows, from the status each action
/// moves the record to.
/// </summary>
internal static class DeliveryWorkflowActions
{
    /// <summary>
    /// The actions whose move <paramref name="workflow"/> allows a record holding
    /// <paramref name="currentStatusId"/> to make, in the order given.
    /// </summary>
    /// <param name="actions">
    /// Each action with the status it moves the record to, resolved as its command resolves it; a
    /// <c>null</c> target, from an alias the workflow lacks, is never allowed.
    /// </param>
    /// <remarks>
    /// Answers for the workflow only. The record's own rules — a version cuts once, a withdrawn one
    /// cannot ship — still decide whether the action succeeds. An action whose target is the status the
    /// record already holds is allowed: the record does not move, so the workflow has nothing to refuse.
    /// </remarks>
    public static List<DeliveryWorkflowActionDto> Allowed(
        StatusWorkflow workflow, Guid currentStatusId, params (DeliveryAction Action, WorkflowStatus? Target)[] actions)
    {
        var allowed = workflow.AllowedTargets(currentStatusId).Select(s => s.Id).ToHashSet();

        return
        [
            .. actions
                .Where(a => a.Target is { } target && (target.Id == currentStatusId || allowed.Contains(target.Id)))
                .Select(a => new DeliveryWorkflowActionDto { Action = a.Action }),
        ];
    }
}
