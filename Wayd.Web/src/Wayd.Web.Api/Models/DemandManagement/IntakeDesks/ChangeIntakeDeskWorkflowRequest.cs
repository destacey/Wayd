using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.DemandManagement.Application.IntakeDesks.Commands;
using Wayd.Web.Api.Models.Admin.StatusWorkflows;

namespace Wayd.Web.Api.Models.DemandManagement.IntakeDesks;

/// <summary>
/// The review workflow a desk's requests should follow, and where requests in each of the current workflow's
/// statuses go.
/// </summary>
public sealed record ChangeIntakeDeskWorkflowRequest
{
    /// <summary>A published intake request workflow, from the workflow choices.</summary>
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// Choices layered over the automatic mapping the workflow change preview shows: a target for each status it
    /// leaves unplaced, plus any automatic choice overridden. Needed only while requests are on the desk.
    /// </summary>
    public List<ReassignWorkflowRequest.StatusRemapDecisionRequest> Decisions { get; set; } = [];

    public ChangeIntakeDeskWorkflowCommand ToChangeIntakeDeskWorkflowCommand(Guid deskId) =>
        new(deskId, WorkflowId,
        [
            .. Decisions.Select(d => new StatusRemapDecisionDto { FromStatusId = d.FromStatusId, ToStatusId = d.ToStatusId }),
        ]);
}

public sealed class ChangeIntakeDeskWorkflowRequestValidator : CustomValidator<ChangeIntakeDeskWorkflowRequest>
{
    public ChangeIntakeDeskWorkflowRequestValidator()
    {
        RuleFor(r => r.WorkflowId).NotEmpty();

        RuleForEach(r => r.Decisions).ChildRules(decision =>
        {
            decision.RuleFor(d => d.FromStatusId).NotEmpty();
            decision.RuleFor(d => d.ToStatusId).NotEmpty();
        });
    }
}
