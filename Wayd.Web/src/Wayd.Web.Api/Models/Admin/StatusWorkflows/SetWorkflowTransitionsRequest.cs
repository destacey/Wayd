using Wayd.Common.Application.StatusWorkflows.Commands;

namespace Wayd.Web.Api.Models.Admin.StatusWorkflows;

/// <summary>
/// A request to replace the moves a draft workflow allows.
/// </summary>
public sealed record SetWorkflowTransitionsRequest
{
    /// <summary>
    /// Every move the workflow should allow. Moves not listed are removed; an empty list removes them all.
    /// </summary>
    public List<WorkflowTransitionRequest> Transitions { get; set; } = [];

    /// <summary>Builds the command for the workflow named in the route.</summary>
    public SetWorkflowTransitionsCommand ToSetWorkflowTransitionsCommand(Guid workflowId)
    {
        return new SetWorkflowTransitionsCommand(
            workflowId, [.. Transitions.Select(t => new WorkflowTransitionPair(t.FromStatusId, t.ToStatusId))]);
    }
}

/// <summary>
/// One move a workflow allows, between two of its statuses.
/// </summary>
public sealed record WorkflowTransitionRequest
{
    /// <summary>The status a record moves out of.</summary>
    public Guid FromStatusId { get; set; }

    /// <summary>The status a record moves into.</summary>
    public Guid ToStatusId { get; set; }
}

/// <summary>Requires both ends of every transition.</summary>
public sealed class SetWorkflowTransitionsRequestValidator : CustomValidator<SetWorkflowTransitionsRequest>
{
    public SetWorkflowTransitionsRequestValidator()
    {
        RuleFor(r => r.Transitions).NotNull();
        RuleForEach(r => r.Transitions).ChildRules(t =>
        {
            t.RuleFor(p => p.FromStatusId).NotEmpty();
            t.RuleFor(p => p.ToStatusId).NotEmpty();
        });
    }
}
