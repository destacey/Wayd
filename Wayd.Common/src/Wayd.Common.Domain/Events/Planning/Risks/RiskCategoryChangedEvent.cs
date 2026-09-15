using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Risks;

/// <summary>
/// A risk was moved to a different ROAM category: resolved, owned, accepted or mitigated.
/// </summary>
public sealed record RiskCategoryChangedEvent : DomainEvent<RiskCategoryChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public RiskCategoryChangedEvent(
        Guid id,
        int key,
        RiskCategory previousCategory,
        RiskCategory category,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousCategory = previousCategory;
        Category = category;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public RiskCategory PreviousCategory { get; }
    public RiskCategory Category { get; }

    [JsonIgnore]
    public string AggregateType => "Risk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
