using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Risks;

/// <summary>
/// A risk's follow-up date moved, was set, or was cleared.
/// </summary>
public sealed record RiskFollowUpDateChangedEvent : DomainEvent<RiskFollowUpDateChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    [JsonConstructor]
    public RiskFollowUpDateChangedEvent(
        Guid id,
        int key,
        LocalDate? previousFollowUpDate,
        LocalDate? followUpDate,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousFollowUpDate = previousFollowUpDate;
        FollowUpDate = followUpDate;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public LocalDate? PreviousFollowUpDate { get; }
    public LocalDate? FollowUpDate { get; }

    [JsonIgnore]
    public string AggregateType => "Risk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
