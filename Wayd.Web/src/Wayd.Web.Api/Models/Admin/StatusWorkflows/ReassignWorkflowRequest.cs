using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Application.StatusWorkflows.Dtos;

namespace Wayd.Web.Api.Models.Admin.StatusWorkflows;

/// <summary>
/// A request to move a scope onto another workflow and bring its records with it.
/// </summary>
public sealed record ReassignWorkflowRequest
{
    /// <summary>
    /// The workflow the scope should move onto.
    /// </summary>
    public Guid TargetWorkflowId { get; set; }

    /// <summary>
    /// Operator choices layered over the automatic mapping — the statuses it could not decide, plus
    /// any automatic choice the operator overrode.
    /// </summary>
    public List<StatusRemapDecisionRequest> Decisions { get; set; } = [];

    public ReassignWorkflowCommand ToReassignWorkflowCommand(Guid assignmentId)
    {
        var decisions = Decisions
            .Select(d => new StatusRemapDecisionDto
            {
                FromStatusId = d.FromStatusId,
                ToStatusId = d.ToStatusId,
            })
            .ToList();

        return new ReassignWorkflowCommand(assignmentId, TargetWorkflowId, decisions);
    }

    /// <summary>One operator decision: send this source status's records to that target status.</summary>
    public sealed record StatusRemapDecisionRequest
    {
        /// <summary>The status of the current workflow whose records are being moved.</summary>
        public Guid FromStatusId { get; set; }

        /// <summary>The status of the target workflow those records should land on.</summary>
        public Guid ToStatusId { get; set; }
    }
}

public sealed class ReassignWorkflowRequestValidator : CustomValidator<ReassignWorkflowRequest>
{
    public ReassignWorkflowRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.TargetWorkflowId).NotEmpty();

        RuleForEach(r => r.Decisions).ChildRules(decision =>
        {
            decision.RuleFor(d => d.FromStatusId).NotEmpty();
            decision.RuleFor(d => d.ToStatusId).NotEmpty();
        });
    }
}
