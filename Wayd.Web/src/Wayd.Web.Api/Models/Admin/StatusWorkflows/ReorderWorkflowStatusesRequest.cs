using Wayd.Common.Application.StatusWorkflows.Commands;

namespace Wayd.Web.Api.Models.Admin.StatusWorkflows;

public sealed record ReorderWorkflowStatusesRequest
{
    /// <summary>
    /// Every status of the workflow, in the order wanted. A partial list is refused.
    /// </summary>
    public List<Guid> OrderedStatusIds { get; set; } = [];

    public ReorderWorkflowStatusesCommand ToReorderWorkflowStatusesCommand(Guid workflowId)
    {
        return new ReorderWorkflowStatusesCommand(workflowId, OrderedStatusIds);
    }
}

public sealed class ReorderWorkflowStatusesRequestValidator : CustomValidator<ReorderWorkflowStatusesRequest>
{
    public ReorderWorkflowStatusesRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.OrderedStatusIds).NotEmpty();
    }
}
