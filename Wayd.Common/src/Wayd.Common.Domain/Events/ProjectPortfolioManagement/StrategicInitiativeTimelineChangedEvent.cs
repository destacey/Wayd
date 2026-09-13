using System.Text.Json.Serialization;
using Wayd.Common.Models;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A strategic initiative's start or end date moved.
/// </summary>
/// <remarks>
/// Raised separately from <see cref="StrategicInitiativeDetailsUpdatedEvent"/> even though one form saves
/// both, matching <see cref="ProgramTimelineChangedEvent"/>: a consumer watching for slippage needs where the
/// dates were as well as where they went, and is not interested in a reworded description.
/// </remarks>
public sealed record StrategicInitiativeTimelineChangedEvent : DomainEvent<StrategicInitiativeTimelineChangedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    [JsonConstructor]
    public StrategicInitiativeTimelineChangedEvent(
        Guid id,
        int key,
        LocalDateRange previousDateRange,
        LocalDateRange dateRange,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousDateRange = previousDateRange;
        DateRange = dateRange;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public LocalDateRange PreviousDateRange { get; }
    public LocalDateRange DateRange { get; }

    [JsonIgnore]
    public string AggregateType => "StrategicInitiative";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
