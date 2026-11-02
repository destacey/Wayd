using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows;

/// <summary>The workflow a record follows, and the level of its scope chain that decided it.</summary>
/// <param name="Workflow">The workflow, with its statuses, transitions and their conditions.</param>
/// <param name="Source">Which assignment, and which part of its scheme, supplied the workflow.</param>
public sealed record ResolvedWorkflow(StatusWorkflow Workflow, WorkflowSource Source);

/// <summary>Where in a <see cref="WorkflowScopeChain"/> a record's workflow came from.</summary>
/// <param name="ScopeId">
/// The scope whose assignment decided, or <c>null</c> when the record follows the organization's. The owning
/// module names the level, since only it knows what its scope ids mean.
/// </param>
/// <param name="IsRecordTypeEntry">
/// Whether the scheme's entry for the record's type decided, rather than the scheme's default.
/// </param>
public sealed record WorkflowSource(Guid? ScopeId, bool IsRecordTypeEntry)
{
    /// <summary>Whether the record follows the organization's assignment, having none nearer.</summary>
    public bool IsOrganization => ScopeId is null;
}
