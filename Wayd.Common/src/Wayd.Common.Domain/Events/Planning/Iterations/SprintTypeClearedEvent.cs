using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>
/// The team cleared the type it had set on the sprint, which goes back to following its planning interval
/// mapping.
/// </summary>
public sealed record SprintTypeClearedEvent : DomainEvent<SprintTypeClearedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public SprintTypeClearedEvent(Guid id, int key, SprintType previousSprintType, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousSprintType = previousSprintType;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The type the team had set, which clearing removed.</summary>
    public SprintType PreviousSprintType { get; }

    [JsonIgnore]
    public string AggregateType => "Iteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
