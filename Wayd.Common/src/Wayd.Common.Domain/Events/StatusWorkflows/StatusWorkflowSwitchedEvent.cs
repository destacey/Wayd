using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A record was moved onto a different workflow, its status translated to the new workflow's equivalent.
/// </summary>
/// <remarks>
/// <para>
/// Raised by the record, not the engine, and shared by every status-tracked aggregate. A switch is not one
/// of the aggregate's own business transitions — nobody released or withdrew anything — so none of their
/// status events describes it, and it happens through a method they all inherit.
/// </para>
/// <para>
/// Deliberately not an <see cref="IAggregateEvent"/>: the activity log attributes it to the entity that
/// raised it, which is the record that moved. <see cref="OwnerType"/> and <see cref="RecordId"/> say the
/// same thing to a consumer reading the payload alone.
/// </para>
/// <para>
/// <see cref="DomainEvent.EventId"/> is the id of the <c>StatusTransition</c> row the switch wrote. The two
/// describe one fact, so sharing an identity lets a backfill replaying transitions into the log stay
/// idempotent.
/// </para>
/// </remarks>
public sealed record StatusWorkflowSwitchedEvent : DomainEvent<StatusWorkflowSwitchedEvent>, IDomainEventDescriptor
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    [JsonConstructor]
    public StatusWorkflowSwitchedEvent(
        // Named for the EventId property it binds to; any other name fails deserialization.
        Guid eventId,
        string ownerType,
        Guid recordId,
        Guid fromWorkflowId,
        Guid fromStatusId,
        StatusCategory fromCategory,
        int fromAlias,
        Guid toWorkflowId,
        Guid toStatusId,
        StatusCategory toCategory,
        int toAlias,
        string? reason,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        EventId = eventId;

        OwnerType = ownerType;
        RecordId = recordId;
        FromWorkflowId = fromWorkflowId;
        FromStatusId = fromStatusId;
        FromCategory = fromCategory;
        FromAlias = fromAlias;
        ToWorkflowId = toWorkflowId;
        ToStatusId = toStatusId;
        ToCategory = toCategory;
        ToAlias = toAlias;
        Reason = reason;

        Timestamp = timestamp;
    }

    /// <summary>The registered owner type of the record that moved, e.g. <c>delivery.release</c>.</summary>
    public string OwnerType { get; }

    public Guid RecordId { get; }

    public Guid FromWorkflowId { get; }
    public Guid FromStatusId { get; }
    public StatusCategory FromCategory { get; }

    /// <summary>
    /// The well-known meaning of the status moved out of, or <see cref="StatusWorkflow.NoAlias"/> for none. An <c>int</c> because the
    /// alias enum belongs to the module that owns <see cref="OwnerType"/>.
    /// </summary>
    public int FromAlias { get; }

    public Guid ToWorkflowId { get; }
    public Guid ToStatusId { get; }
    public StatusCategory ToCategory { get; }

    /// <inheritdoc cref="FromAlias"/>
    public int ToAlias { get; }

    public string? Reason { get; }
}
