using Wayd.Common.Domain.Data;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// The workflow one record type follows under a <see cref="StatusWorkflowScheme"/>. A type with no entry
/// follows the scheme's default.
/// </summary>
/// <remarks>Created, changed and removed through the owning scheme, never directly.</remarks>
public sealed class StatusWorkflowSchemeEntry : BaseAuditableEntity
{
    private StatusWorkflowSchemeEntry() { }

    internal StatusWorkflowSchemeEntry(Guid schemeId, Guid recordTypeId, Guid workflowId)
    {
        SchemeId = schemeId;
        RecordTypeId = recordTypeId;
        WorkflowId = workflowId;
    }

    /// <summary>The scheme this entry belongs to.</summary>
    public Guid SchemeId { get; private init; }

    /// <summary>The record type it sends to <see cref="WorkflowId"/>. At most one entry per type per scheme.</summary>
    public Guid RecordTypeId { get; private init; }

    /// <summary>The workflow records of that type follow under the scheme.</summary>
    public Guid WorkflowId { get; internal set; }
}
