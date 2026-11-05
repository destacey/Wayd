using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Integrations.AzureDevOps.Models.Contracts;

namespace Wayd.Integrations.AzureDevOps.Models.Processes;

internal sealed record ProcessWorkItemStateDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string StateCategory { get; set; }
    public int Order { get; set; }
    public bool Hidden { get; set; }
}

internal static class ProcessWorkItemStateDtoExtensions
{
    public static AzdoWorkStatus ToAzdoWorkStatus(this ProcessWorkItemStateDto workItemState)
    {
        return new AzdoWorkStatus
        {
            Name = workItemState.Name
        };
    }

    public static AzdoWorkflowState ToAzdoWorkflowState(this ProcessWorkItemStateDto workItemState)
    {
        return new AzdoWorkflowState
        {
            StatusName = workItemState.Name,
            Category = workItemState.StateCategory switch
            {
                "Proposed" => StatusCategory.Proposed,
                "InProgress" => StatusCategory.Active,
                "Resolved" => StatusCategory.Active,
                "Completed" => StatusCategory.Done,
                "Removed" => StatusCategory.Removed,
                _ => throw new ArgumentOutOfRangeException(nameof(workItemState.StateCategory), workItemState.StateCategory, null)
            },
            Order = workItemState.Order,
            IsActive = !workItemState.Hidden
        };
    }
}
