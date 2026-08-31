using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Web.Api.Models.Admin.StatusWorkflows;

/// <summary>
/// A request to change a status's category and well-known meaning, which records carry denormalized.
/// </summary>
public sealed record ReclassifyWorkflowStatusRequest
{
    /// <summary>
    /// The high-level bucket the status belongs to.
    /// </summary>
    public StatusCategory Category { get; set; }

    /// <summary>
    /// The well-known meaning the status carries within its owner type, or zero for none.
    /// </summary>
    public int Alias { get; set; }

    public ReclassifyWorkflowStatusCommand ToReclassifyWorkflowStatusCommand(Guid workflowId, Guid statusId)
    {
        return new ReclassifyWorkflowStatusCommand(workflowId, statusId, Category, Alias);
    }
}

public sealed class ReclassifyWorkflowStatusRequestValidator : CustomValidator<ReclassifyWorkflowStatusRequest>
{
    public ReclassifyWorkflowStatusRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Category).IsInEnum();
    }
}
