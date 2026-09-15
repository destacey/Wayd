using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// An output was removed from a scoring model.
/// </summary>
/// <remarks>
/// The name and token are carried because the output is gone by the time anyone reads the entry, and later formulas referred to it by token.
/// </remarks>
public sealed record ScoringModelOutputRemovedEvent : DomainEvent<ScoringModelOutputRemovedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ScoringModelOutputRemovedEvent(
        Guid id,
        int key,
        Guid outputId,
        string name,
        string token,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        OutputId = outputId;
        Name = name;
        Token = token;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid OutputId { get; }
    public string Name { get; }
    public string Token { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
