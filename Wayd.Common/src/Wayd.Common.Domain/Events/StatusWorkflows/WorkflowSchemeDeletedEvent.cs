using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A workflow scheme nothing pointed at was deleted.
/// </summary>
public sealed record WorkflowSchemeDeletedEvent : DomainEvent<WorkflowSchemeDeletedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    [JsonConstructor]
    public WorkflowSchemeDeletedEvent(Guid id, int key, string ownerType, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        OwnerType = ownerType;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string OwnerType { get; }

    [JsonIgnore]
    public string AggregateType => "WorkflowScheme";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
