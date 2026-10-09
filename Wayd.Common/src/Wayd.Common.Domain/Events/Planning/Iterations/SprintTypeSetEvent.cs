using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>
/// The team set the sprint's type, which then holds whatever its planning interval mapping says.
/// </summary>
public sealed record SprintTypeSetEvent : DomainEvent<SprintTypeSetEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public SprintTypeSetEvent(Guid id, int key, SprintType? fromSprintType, SprintType toSprintType, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromSprintType = fromSprintType;
        ToSprintType = toSprintType;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The type the team had set before, or null when the sprint followed its mapping.</summary>
    public SprintType? FromSprintType { get; }

    public SprintType ToSprintType { get; }

    [JsonIgnore]
    public string AggregateType => "Iteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
