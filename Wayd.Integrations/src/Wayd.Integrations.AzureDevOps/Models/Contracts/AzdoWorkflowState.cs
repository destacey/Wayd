using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Integrations.AzureDevOps.Models.Contracts;

public sealed record AzdoWorkflowState : IExternalWorkflowState
{
    /// <summary>The state's id, which a rename leaves unchanged.</summary>
    public required string ExternalKey { get; set; }
    public required string StatusName { get; set; }
    public StatusCategory Category { get; set; }
    public int Order { get; set; }
    public bool IsActive { get; set; }
}
