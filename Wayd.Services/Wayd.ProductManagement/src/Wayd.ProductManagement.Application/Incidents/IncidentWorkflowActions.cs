using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Application.Incidents.Dtos;

namespace Wayd.ProductManagement.Application.Incidents;

/// <summary>
/// Answers which lifecycle actions an incident's workflow allows from the status it holds.
/// </summary>
internal static class IncidentWorkflowActions
{
    public static List<IncidentWorkflowActionDto> Allowed(
        StatusWorkflow workflow,
        Guid currentStatusId,
        params (IncidentWorkflowAction Action, WorkflowStatus? Target)[] actions)
    {
        var allowed = workflow.AllowedTargets(currentStatusId).Select(s => s.Id).ToHashSet();

        return
        [
            .. actions
                .Where(a => a.Target is { } target && (target.Id == currentStatusId || allowed.Contains(target.Id)))
                .Select(a => new IncidentWorkflowActionDto { Action = a.Action }),
        ];
    }
}
