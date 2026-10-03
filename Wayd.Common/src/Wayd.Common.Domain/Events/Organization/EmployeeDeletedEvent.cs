using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// An invalid employee was removed, and the people source is barred from importing them again.
/// </summary>
public sealed record EmployeeDeletedEvent : DomainEvent<EmployeeDeletedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    [JsonConstructor]
    public EmployeeDeletedEvent(Guid id, int key, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    [JsonIgnore]
    public string AggregateType => "Employee";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
