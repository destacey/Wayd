using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A scope stopped having a workflow of its own: its records now follow the organization's, or the scope
/// itself is gone.
/// </summary>
/// <remarks>
/// Only a narrower scope's assignment is ever removed; the organization-level one is the fallback every
/// scope relies on and is never removed. Like <see cref="WorkflowAssignedEvent"/>, it has no owning record
/// to announce it, so the engine does.
/// </remarks>
public sealed record WorkflowAssignmentRemovedEvent : DomainEvent<WorkflowAssignmentRemovedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public WorkflowAssignmentRemovedEvent(
        string ownerType,
        Guid scopeId,
        Guid fromWorkflowId,
        Guid? toWorkflowId,
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
        FromSchemeId = fromSchemeId;
        ToSchemeId = toSchemeId;

        Timestamp = timestamp;
    }

    /// <summary>The kind of record whose governing workflow changed.</summary>
    public string OwnerType { get; }

    /// <summary>The scope that no longer has a workflow of its own.</summary>
    public Guid ScopeId { get; }

    /// <summary>The default workflow of the scheme the scope had chosen.</summary>
    public Guid FromWorkflowId { get; }

    /// <summary>
    /// The organization's workflow the scope follows from now on, or <c>null</c> when the scope itself was
    /// removed and follows nothing.
    /// </summary>
    public Guid? ToWorkflowId { get; }

    /// <summary>The scheme the scope had chosen. Added in 1.1; null in earlier payloads.</summary>
    public Guid? FromSchemeId { get; }

    /// <summary>
    /// The organization's scheme the scope follows from now on, or <c>null</c> when the scope itself was removed.
    /// Added in 1.1; null in earlier payloads.
    /// </summary>
    public Guid? ToSchemeId { get; }

    [JsonIgnore]
    public string AggregateType => "Workflow";
    [JsonIgnore]
    public Guid AggregateId => FromWorkflowId;
}
