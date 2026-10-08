using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// A holiday calendar was renamed or its description changed.
/// </summary>
public sealed record HolidayCalendarDetailsUpdatedEvent : DomainEvent<HolidayCalendarDetailsUpdatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public HolidayCalendarDetailsUpdatedEvent(Guid id, int key, string name, string? description, string previousName, string? previousDescription, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        PreviousName = previousName;
        PreviousDescription = previousDescription;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string? Description { get; }
    public string PreviousName { get; }
    public string? PreviousDescription { get; }

    [JsonIgnore]
    public string AggregateType => "HolidayCalendar";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
