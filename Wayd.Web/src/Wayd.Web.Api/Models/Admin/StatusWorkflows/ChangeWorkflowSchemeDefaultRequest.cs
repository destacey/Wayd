using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Application.StatusWorkflows.Dtos;

namespace Wayd.Web.Api.Models.Admin.StatusWorkflows;

/// <summary>A request to change the workflow followed by every record whose type has no entry in a scheme.</summary>
public sealed record ChangeWorkflowSchemeDefaultRequest
{
    /// <summary>The published workflow those records follow from now on.</summary>
    public Guid WorkflowId { get; set; }

    /// <summary>Operator choices layered over the automatic mapping, for the statuses whose records move. Needed only when records move.</summary>
    public List<ReassignWorkflowRequest.StatusRemapDecisionRequest> Decisions { get; set; } = [];

    public ChangeWorkflowSchemeDefaultCommand ToChangeWorkflowSchemeDefaultCommand(Guid schemeId) =>
        new(schemeId, WorkflowId, Decisions.Select(d => new StatusRemapDecisionDto { FromStatusId = d.FromStatusId, ToStatusId = d.ToStatusId }).ToList());
}

public sealed class ChangeWorkflowSchemeDefaultRequestValidator : CustomValidator<ChangeWorkflowSchemeDefaultRequest>
{
    public ChangeWorkflowSchemeDefaultRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.WorkflowId).NotEmpty();
        RuleForEach(r => r.Decisions).ChildRules(decision =>
        {
            decision.RuleFor(d => d.FromStatusId).NotEmpty();
            decision.RuleFor(d => d.ToStatusId).NotEmpty();
        });
    }
}
