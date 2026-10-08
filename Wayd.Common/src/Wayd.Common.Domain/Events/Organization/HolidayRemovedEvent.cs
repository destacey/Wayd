using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// A holiday was removed from a holiday calendar. Teams using the calendar work that day again.
/// </summary>
public sealed record HolidayRemovedEvent : DomainEvent<HolidayRemovedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public HolidayRemovedEvent(Guid id, int key, Guid holidayId, LocalDate date, string name, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        HolidayId = holidayId;
        Date = date;
        Name = name;

        Timestamp = timestamp;
    }

    /// <summary>The calendar's id.</summary>
    public Guid Id { get; }

    /// <summary>The calendar's key.</summary>
    public int Key { get; }

    public Guid HolidayId { get; }
    public LocalDate Date { get; }
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "HolidayCalendar";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
