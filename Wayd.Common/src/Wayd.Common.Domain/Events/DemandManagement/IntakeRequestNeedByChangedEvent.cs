using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake request's need-by date, its basis or its reference changed, or the date was set or cleared.
/// </summary>
/// <remarks>
/// The date, basis and reference change together, so one event carries all of them at both ends. Clearing the date clears the rest: <see cref="To"/> is null.
/// </remarks>
public sealed record IntakeRequestNeedByChangedEvent : DomainEvent<IntakeRequestNeedByChangedEvent>, IDomainEventDescriptor, IIntakeRequestEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    public static IntakeRequestReadTier ReadTier => IntakeRequestReadTier.Summary;

    public IntakeRequestNeedByChangedEvent(Guid id, int key, IntakeRequestNeedByDetails? from, IntakeRequestNeedByDetails? to, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        From = from;
        To = to;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    /// <summary>The need-by before the change, or null when there was none.</summary>
    public IntakeRequestNeedByDetails? From { get; }
    /// <summary>The need-by after the change, or null when it was cleared.</summary>
    public IntakeRequestNeedByDetails? To { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
