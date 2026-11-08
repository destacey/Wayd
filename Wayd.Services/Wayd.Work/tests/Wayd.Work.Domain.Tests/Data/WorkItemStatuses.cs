using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Domain.Models;

namespace Wayd.Work.Domain.Tests.Data;

/// <summary>Engine statuses for work item tests.</summary>
public static class WorkItemStatuses
{
    /// <summary>A status of its own workflow, or of <paramref name="workflowId"/>.</summary>
    public static StatusRef Create(string name = "New", StatusCategory category = StatusCategory.Proposed, Guid? workflowId = null, Guid? statusId = null) =>
        new(workflowId ?? Guid.NewGuid(), statusId ?? Guid.NewGuid(), name, category, StatusWorkflow.NoAlias);

    /// <summary>The status the work item holds, as a reference to apply again.</summary>
    public static StatusRef CurrentStatus(this WorkItem workItem) =>
        new(workItem.StatusWorkflowId, workItem.StatusId, workItem.StatusName, workItem.StatusCategory, workItem.StatusAliasValue);
}
