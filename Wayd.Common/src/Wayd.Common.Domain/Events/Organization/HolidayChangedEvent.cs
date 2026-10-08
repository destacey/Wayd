using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// A holiday in a holiday calendar was moved to another date or renamed.
/// </summary>
public sealed record HolidayChangedEvent : DomainEvent<HolidayChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public HolidayChangedEvent(Guid id, int key, Guid holidayId, LocalDate date, string name, LocalDate previousDate, string previousName, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        HolidayId = holidayId;
        Date = date;
        Name = name;
        PreviousDate = previousDate;
        PreviousName = previousName;

        Timestamp = timestamp;
    }

    /// <summary>The calendar's id.</summary>
    public Guid Id { get; }

    /// <summary>The calendar's key.</summary>
    public int Key { get; }

    public Guid HolidayId { get; }
    public LocalDate Date { get; }
    public string Name { get; }
    public LocalDate PreviousDate { get; }
    public string PreviousName { get; }

    [JsonIgnore]
    public string AggregateType => "HolidayCalendar";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
