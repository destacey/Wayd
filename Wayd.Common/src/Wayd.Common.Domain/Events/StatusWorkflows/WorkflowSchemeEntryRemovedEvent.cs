using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A record type's entry was removed from a scheme, so its records follow the scheme's default again.
/// </summary>
public sealed record WorkflowSchemeEntryRemovedEvent : DomainEvent<WorkflowSchemeEntryRemovedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public WorkflowSchemeEntryRemovedEvent(
        Guid id,
        int key,
        Guid recordTypeId,
        Guid fromWorkflowId,
        Guid toWorkflowId,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        RecordTypeId = recordTypeId;
        FromWorkflowId = fromWorkflowId;
        ToWorkflowId = toWorkflowId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The record type whose entry was removed.</summary>
    public Guid RecordTypeId { get; }

    /// <summary>The workflow the removed entry named.</summary>
    public Guid FromWorkflowId { get; }

    /// <summary>The scheme's default, which the type follows now.</summary>
    public Guid ToWorkflowId { get; }

    [JsonIgnore]
    public string AggregateType => "WorkflowScheme";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
