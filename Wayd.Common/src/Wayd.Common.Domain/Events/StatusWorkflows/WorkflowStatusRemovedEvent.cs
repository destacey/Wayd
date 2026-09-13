using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A status was removed from a draft workflow.
/// </summary>
/// <remarks>
/// The name is carried because the status is gone by the time anyone reads the entry.
/// </remarks>
public sealed record WorkflowStatusRemovedEvent : DomainEvent<WorkflowStatusRemovedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public WorkflowStatusRemovedEvent(
        Guid id,
        int key,
        Guid statusId,
        string name,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        StatusId = statusId;
        Name = name;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid StatusId { get; }
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "Workflow";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
