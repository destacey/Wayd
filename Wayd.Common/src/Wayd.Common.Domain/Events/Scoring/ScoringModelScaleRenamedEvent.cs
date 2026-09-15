using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// A scoring model's rating scale was renamed.
/// </summary>
public sealed record ScoringModelScaleRenamedEvent : DomainEvent<ScoringModelScaleRenamedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ScoringModelScaleRenamedEvent(
        Guid id,
        int key,
        Guid scaleId,
        string previousName,
        string name,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ScaleId = scaleId;
        PreviousName = previousName;
        Name = name;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ScaleId { get; }
    public string PreviousName { get; }
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
