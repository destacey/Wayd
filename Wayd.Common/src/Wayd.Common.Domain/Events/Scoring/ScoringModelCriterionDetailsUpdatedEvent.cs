using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// A criterion's name, token or description was edited.
/// </summary>
/// <remarks>
/// Its weight and scale change for different reasons and have their own events.
/// </remarks>
public sealed record ScoringModelCriterionDetailsUpdatedEvent : DomainEvent<ScoringModelCriterionDetailsUpdatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ScoringModelCriterionDetailsUpdatedEvent(
        Guid id,
        int key,
        Guid criterionId,
        string name,
        string token,
        string? description,
        ScoringCriterionDetails previous,
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
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid CriterionId { get; }
    public string Name { get; }
    public string Token { get; }
    public string? Description { get; }

    /// <summary>The details this edit replaced.</summary>
    public ScoringCriterionDetails Previous { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
