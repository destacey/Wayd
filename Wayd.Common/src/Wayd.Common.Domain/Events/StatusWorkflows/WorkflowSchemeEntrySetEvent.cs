using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// Records of one record type now follow a different workflow, in every scope on the scheme.
/// </summary>
public sealed record WorkflowSchemeEntrySetEvent : DomainEvent<WorkflowSchemeEntrySetEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public WorkflowSchemeEntrySetEvent(
        Guid id,
        int key,
        Guid recordTypeId,
        Guid fromWorkflowId,
        Guid toWorkflowId,
        bool hadEntry,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        RecordTypeId = recordTypeId;
        FromWorkflowId = fromWorkflowId;
        ToWorkflowId = toWorkflowId;
        HadEntry = hadEntry;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The record type whose workflow changed.</summary>
    public Guid RecordTypeId { get; }

    /// <summary>The workflow the type followed before: its previous entry's, or the scheme's default.</summary>
    public Guid FromWorkflowId { get; }

    /// <summary>The workflow the type follows now.</summary>
    public Guid ToWorkflowId { get; }

    /// <summary>Whether the type had an entry before, rather than following the default.</summary>
    public bool HadEntry { get; }

    [JsonIgnore]
    public string AggregateType => "WorkflowScheme";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
