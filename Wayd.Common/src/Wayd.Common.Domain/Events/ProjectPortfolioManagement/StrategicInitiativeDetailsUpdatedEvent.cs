using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A strategic initiative's name or description was edited.
/// </summary>
public sealed record StrategicInitiativeDetailsUpdatedEvent : DomainEvent<StrategicInitiativeDetailsUpdatedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public StrategicInitiativeDetailsUpdatedEvent(
        Guid id,
        int key,
        string name,
        string description,
        StrategicInitiativeDetails previous,
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
    public string Description { get; }

    /// <summary>The details this edit replaced.</summary>
    public StrategicInitiativeDetails Previous { get; }

    [JsonIgnore]
    public string AggregateType => "StrategicInitiative";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
