using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// A criterion was added to a scoring model.
/// </summary>
public sealed record ScoringModelCriterionAddedEvent : DomainEvent<ScoringModelCriterionAddedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ScoringModelCriterionAddedEvent(
        Guid id,
        int key,
        Guid criterionId,
        string name,
        string token,
        string? description,
        decimal? weight,
        Guid? scaleId,
        int order,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        CriterionId = criterionId;
        Name = name;
        Token = token;
        Description = description;
        Weight = weight;
        ScaleId = scaleId;
        Order = order;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid CriterionId { get; }
    public string Name { get; }
    public string Token { get; }
    public string? Description { get; }
    public decimal? Weight { get; }

    /// <summary>The scale it is rated against, or null for free numeric entry.</summary>
    public Guid? ScaleId { get; }

    public int Order { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
