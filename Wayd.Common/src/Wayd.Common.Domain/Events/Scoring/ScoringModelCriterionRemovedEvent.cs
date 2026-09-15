using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// A criterion was removed from a scoring model.
/// </summary>
/// <remarks>
/// The name and token are carried because the criterion is gone by the time anyone reads the entry, and formulas referred to it by token.
/// </remarks>
public sealed record ScoringModelCriterionRemovedEvent : DomainEvent<ScoringModelCriterionRemovedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ScoringModelCriterionRemovedEvent(
        Guid id,
        int key,
        Guid criterionId,
        string name,
        string token,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        CriterionId = criterionId;
        Name = name;
        Token = token;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid CriterionId { get; }
    public string Name { get; }
    public string Token { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
