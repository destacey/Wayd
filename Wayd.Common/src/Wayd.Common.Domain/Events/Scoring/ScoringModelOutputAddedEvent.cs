using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// An output formula was added to a scoring model.
/// </summary>
/// <remarks>
/// Whether it became the primary output is recorded by <see cref="ScoringModelPrimaryOutputChangedEvent"/>.
/// </remarks>
public sealed record ScoringModelOutputAddedEvent : DomainEvent<ScoringModelOutputAddedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ScoringModelOutputAddedEvent(
        Guid id,
        int key,
        Guid outputId,
        string name,
        string token,
        string formula,
        int order,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        OutputId = outputId;
        Name = name;
        Token = token;
        Formula = formula;
        Order = order;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid OutputId { get; }
    public string Name { get; }
    public string Token { get; }
    public string Formula { get; }
    public int Order { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
