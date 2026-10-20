using Wayd.Common.Domain.Data;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// A move the workflow allows.
/// </summary>
/// <remarks>
/// A record holding <see cref="FromStatusId"/> may move to <see cref="ToStatusId"/>. Not to be confused
/// with <see cref="StatusTransition"/>, a move a record made. Consulted only
/// when the workflow's <see cref="StatusWorkflow.TransitionMode"/> is
/// <see cref="Enums.TransitionMode.Restricted"/>.
/// <para>
/// An entity with its own id rather than a value pair so that conditions on a move can attach to it.
/// Created and removed through the owning <see cref="StatusWorkflow"/>, never directly.
/// </para>
/// </remarks>
public sealed class WorkflowTransition : BaseAuditableEntity
{
    private WorkflowTransition() { }

    internal WorkflowTransition(Guid workflowId, Guid fromStatusId, Guid toStatusId)
    {
        WorkflowId = workflowId;
        FromStatusId = fromStatusId;
        ToStatusId = toStatusId;
    }

    /// <summary>The workflow that allows this move. Both statuses belong to it.</summary>
    public Guid WorkflowId { get; private init; }

    /// <summary>The status a record moves out of.</summary>
    public Guid FromStatusId { get; private init; }

    /// <summary>The status a record moves into.</summary>
    public Guid ToStatusId { get; private init; }
}
