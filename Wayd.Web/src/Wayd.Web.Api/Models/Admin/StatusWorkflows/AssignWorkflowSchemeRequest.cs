using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Application.StatusWorkflows.Dtos;

namespace Wayd.Web.Api.Models.Admin.StatusWorkflows;

/// <summary>A request to point a scope at another workflow scheme and move the records whose workflow changes.</summary>
public sealed record AssignWorkflowSchemeRequest
{
    /// <summary>The scheme the scope should follow.</summary>
    public Guid SchemeId { get; set; }

    /// <summary>Operator choices layered over the automatic mapping, for the statuses whose records move. Needed only when records move.</summary>
    public List<ReassignWorkflowRequest.StatusRemapDecisionRequest> Decisions { get; set; } = [];

    public AssignWorkflowSchemeCommand ToAssignWorkflowSchemeCommand(Guid assignmentId) =>
        new(assignmentId, SchemeId, Decisions.Select(d => new StatusRemapDecisionDto { FromStatusId = d.FromStatusId, ToStatusId = d.ToStatusId }).ToList());
}

public sealed class AssignWorkflowSchemeRequestValidator : CustomValidator<AssignWorkflowSchemeRequest>
{
    public AssignWorkflowSchemeRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.SchemeId).NotEmpty();
        RuleForEach(r => r.Decisions).ChildRules(decision =>
        {
            decision.RuleFor(d => d.FromStatusId).NotEmpty();
            decision.RuleFor(d => d.ToStatusId).NotEmpty();
        });
    }
}
