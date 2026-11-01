using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Application.StatusWorkflows.Dtos;

namespace Wayd.Web.Api.Models.Admin.StatusWorkflows;

/// <summary>A request to change which workflow one record type follows under a scheme.</summary>
public sealed record SetWorkflowSchemeEntryRequest
{
    /// <summary>The published workflow the record type follows from now on, or null to remove its entry so it follows the scheme's default.</summary>
    public Guid? WorkflowId { get; set; }

    /// <summary>Operator choices layered over the automatic mapping, for the statuses whose records move. Needed only when records move.</summary>
    public List<ReassignWorkflowRequest.StatusRemapDecisionRequest> Decisions { get; set; } = [];

    public SetWorkflowSchemeEntryCommand ToSetWorkflowSchemeEntryCommand(Guid schemeId, Guid recordTypeId) =>
        new(schemeId, recordTypeId, WorkflowId, Decisions.Select(d => new StatusRemapDecisionDto { FromStatusId = d.FromStatusId, ToStatusId = d.ToStatusId }).ToList());
}

public sealed class SetWorkflowSchemeEntryRequestValidator : CustomValidator<SetWorkflowSchemeEntryRequest>
{
    public SetWorkflowSchemeEntryRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.WorkflowId).NotEqual(Guid.Empty);
        RuleForEach(r => r.Decisions).ChildRules(decision =>
        {
            decision.RuleFor(d => d.FromStatusId).NotEmpty();
            decision.RuleFor(d => d.ToStatusId).NotEmpty();
        });
    }
}
