using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// A holiday calendar was created, with no holidays yet.
/// </summary>
public sealed record HolidayCalendarCreatedEvent : DomainEvent<HolidayCalendarCreatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    public HolidayCalendarCreatedEvent(Guid id, int key, string name, string? description, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string? Description { get; }

    [JsonIgnore]
    public string AggregateType => "HolidayCalendar";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
