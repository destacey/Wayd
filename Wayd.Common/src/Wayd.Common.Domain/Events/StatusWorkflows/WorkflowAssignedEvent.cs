using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A scope's records are now governed by a different workflow.
/// </summary>
/// <remarks>
/// The engine's only event, because it is the only engine change no owning object can announce. Every
/// status change is already raised by the aggregate it happened to, in business terms —
/// <c>ReleaseWithdrawn</c>, <c>DeploymentRolledBack</c> — so an engine-level status event would
/// double-fire on each one.
/// <para>
/// An assignment has no such owner: it is keyed by <see cref="OwnerType"/> and <see cref="ScopeId"/>
/// rather than being a property of the container, so the portfolio whose projects just changed
/// workflow never learns of it. The consequences are also cross-cutting — cached workflow lookups go
/// stale, and once the remap engine exists this is what starts migrating every record in the scope.
/// </para>
/// </remarks>
public sealed record WorkflowAssignedEvent : DomainEvent
{
    public WorkflowAssignedEvent(
        string ownerType,
        Guid? scopeId,
        Guid? fromWorkflowId,
        Guid toWorkflowId,
        string toWorkflowName,
        EventActor actor,
        Instant timestamp)
        : base(actor)
    {
        OwnerType = ownerType;
        ScopeId = scopeId;
        FromWorkflowId = fromWorkflowId;
        ToWorkflowId = toWorkflowId;
        ToWorkflowName = toWorkflowName;

        Timestamp = timestamp;
    }

    /// <summary>The kind of record whose governing workflow changed.</summary>
    public string OwnerType { get; }

    /// <summary>The scope affected, or <c>null</c> for the organization-level default.</summary>
    public Guid? ScopeId { get; }

    /// <summary>
    /// The workflow the scope was on, or <c>null</c> when it is being assigned for the first time.
    /// </summary>
    public Guid? FromWorkflowId { get; }

    /// <summary>The workflow the scope now uses.</summary>
    public Guid ToWorkflowId { get; }

    /// <summary>
    /// Its name at the time, so a notification renders without a query and stays accurate after a
    /// later rename.
    /// </summary>
    public string ToWorkflowName { get; }
}
