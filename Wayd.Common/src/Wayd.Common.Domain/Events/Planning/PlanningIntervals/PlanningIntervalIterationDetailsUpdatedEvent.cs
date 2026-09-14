using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervals;

/// <summary>
/// An iteration of a planning interval was renamed or recategorized.
/// </summary>
public sealed record PlanningIntervalIterationDetailsUpdatedEvent : DomainEvent<PlanningIntervalIterationDetailsUpdatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public PlanningIntervalIterationDetailsUpdatedEvent(
        Guid id,
        int key,
        Guid iterationId,
        string name,
        IterationCategory category,
        PlanningIntervalIterationDetails? previous,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        IterationId = iterationId;
        Name = name;
        Category = category;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid IterationId { get; }
    public string Name { get; }
    public IterationCategory Category { get; }

    /// <summary>The details this change replaced. Null only on a payload that did not record them.</summary>
    public PlanningIntervalIterationDetails? Previous { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningInterval";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
