using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// A holiday calendar was deleted, with its holidays. No operating model used it, and it was not the default.
/// </summary>
public sealed record HolidayCalendarDeletedEvent : DomainEvent<HolidayCalendarDeletedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    public HolidayCalendarDeletedEvent(Guid id, int key, string name, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "HolidayCalendar";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
