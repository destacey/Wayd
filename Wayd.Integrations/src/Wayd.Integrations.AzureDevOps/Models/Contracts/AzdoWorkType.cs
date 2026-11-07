using Wayd.Common.Application.Interfaces.ExternalWork;

namespace Wayd.Integrations.AzureDevOps.Models.Contracts;

public sealed record AzdoWorkType : IExternalWorkTypeWorkflow
{
    /// <summary>The work item type's reference name, which a rename leaves unchanged.</summary>
    public required string ExternalKey { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string WorkTypeLevelId { get; set; }
    public bool IsActive { get; set; }
    public required List<AzdoWorkflowState> WorkflowStates { get; set; } = [];

    IList<IExternalWorkflowState> IExternalWorkTypeWorkflow.WorkflowStates => WorkflowStates.OfType<IExternalWorkflowState>().ToList();
}
