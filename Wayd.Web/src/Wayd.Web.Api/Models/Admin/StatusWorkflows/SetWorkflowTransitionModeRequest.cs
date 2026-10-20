using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Web.Api.Models.Admin.StatusWorkflows;

/// <summary>
/// A request to let a draft workflow's records move between any statuses, or only along its transitions.
/// </summary>
public sealed record SetWorkflowTransitionModeRequest
{
    /// <summary>
    /// <c>AnyToAny</c> to allow every move, or <c>Restricted</c> to allow only the workflow's transitions.
    /// </summary>
    public TransitionMode Mode { get; set; }

    /// <summary>Builds the command for the workflow named in the route.</summary>
    public SetWorkflowTransitionModeCommand ToSetWorkflowTransitionModeCommand(Guid workflowId)
    {
        return new SetWorkflowTransitionModeCommand(workflowId, Mode);
    }
}

/// <summary>Requires a known mode.</summary>
public sealed class SetWorkflowTransitionModeRequestValidator : CustomValidator<SetWorkflowTransitionModeRequest>
{
    public SetWorkflowTransitionModeRequestValidator()
    {
        RuleFor(r => r.Mode).IsInEnum();
    }
}
