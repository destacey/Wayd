using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;

/// <summary>
/// A planning interval objective's descriptive details taken together, as
/// <see cref="PlanningIntervalObjectiveDetailsUpdatedEvent.Previous"/> records the values a change replaced.
/// </summary>
public sealed record PlanningIntervalObjectiveDetails(string Name, string? Description);

/// <summary>
/// A planning interval objective's name or description changed.
/// </summary>
public sealed record PlanningIntervalObjectiveDetailsUpdatedEvent : DomainEvent<PlanningIntervalObjectiveDetailsUpdatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public PlanningIntervalObjectiveDetailsUpdatedEvent(
        Guid id,
        int key,
        string name,
        string? description,
        PlanningIntervalObjectiveDetails? previous,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string? Description { get; }

    /// <summary>
    /// The details this change replaced. Grouped so that null can only mean "not recorded", as
    /// <c>ProjectDetailsUpdatedEvent.Previous</c> is.
    /// </summary>
    public PlanningIntervalObjectiveDetails? Previous { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningIntervalObjective";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
