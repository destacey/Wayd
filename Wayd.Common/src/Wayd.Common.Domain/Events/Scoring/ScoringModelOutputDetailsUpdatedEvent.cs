using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// An output's name or token was edited.
/// </summary>
/// <remarks>
/// Its formula changes for a different reason and has its own event.
/// </remarks>
public sealed record ScoringModelOutputDetailsUpdatedEvent : DomainEvent<ScoringModelOutputDetailsUpdatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ScoringModelOutputDetailsUpdatedEvent(
        Guid id,
        int key,
        Guid outputId,
        string name,
        string token,
        ScoringOutputDetails previous,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        OutputId = outputId;
        Name = name;
        Token = token;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid OutputId { get; }
    public string Name { get; }
    public string Token { get; }

    /// <summary>The details this edit replaced.</summary>
    public ScoringOutputDetails Previous { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
