using System.Text.Json.Serialization;
using Wayd.Common.Domain.Events;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A scope's records are now governed by a different scheme, and so by its workflows.
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
public sealed record WorkflowAssignedEvent : DomainEvent<WorkflowAssignedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public WorkflowAssignedEvent(
        string ownerType,
        Guid? scopeId,
        Guid? fromWorkflowId,
        Guid toWorkflowId,
        string toWorkflowName,
        Guid? fromSchemeId,
        Guid? toSchemeId,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.1")
    {
        OwnerType = ownerType;
        ScopeId = scopeId;
        FromWorkflowId = fromWorkflowId;
        ToWorkflowId = toWorkflowId;
        ToWorkflowName = toWorkflowName;
        FromSchemeId = fromSchemeId;
        ToSchemeId = toSchemeId;

        Timestamp = timestamp;
    }

    /// <summary>The kind of record whose governing workflow changed.</summary>
    public string OwnerType { get; }

    /// <summary>The scope affected, or <c>null</c> for the organization-level default.</summary>
    public Guid? ScopeId { get; }

    /// <summary>
    /// The default workflow of the scheme the scope was on, or <c>null</c> when it is being assigned for the
    /// first time. Before schemes, the one workflow every record in the scope followed.
    /// </summary>
    public Guid? FromWorkflowId { get; }

    /// <summary>The default workflow of the scheme the scope now follows.</summary>
    public Guid ToWorkflowId { get; }

    /// <summary>
    /// Its name at the time, so a notification renders without a query and stays accurate after a
    /// later rename.
    /// </summary>
    public string ToWorkflowName { get; }

    /// <summary>
    /// The scheme the scope was on. Added in 1.1; null in earlier payloads, written when a scope pointed at a
    /// workflow directly, and on a first assignment.
    /// </summary>
    public Guid? FromSchemeId { get; }

    /// <summary>The scheme the scope now follows. Added in 1.1; null in earlier payloads.</summary>
    public Guid? ToSchemeId { get; }

    [JsonIgnore]
    public string AggregateType => "Workflow";
    [JsonIgnore]
    public Guid AggregateId => ToWorkflowId;
}
