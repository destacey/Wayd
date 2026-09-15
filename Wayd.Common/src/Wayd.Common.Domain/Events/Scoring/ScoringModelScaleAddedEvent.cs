using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// A rating scale was added to a scoring model. It has no levels yet; each is added by its own event.
/// </summary>
public sealed record ScoringModelScaleAddedEvent : DomainEvent<ScoringModelScaleAddedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ScoringModelScaleAddedEvent(
        Guid id,
        int key,
        Guid scaleId,
        string name,
        int order,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ScaleId = scaleId;
        Name = name;
        Order = order;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ScaleId { get; }
    public string Name { get; }
    public int Order { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
