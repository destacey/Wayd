using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>
/// The iteration moved between Future, Active and Completed.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised: state is worked out from the iteration's dates when read, so
/// nothing changes on the record when it moves. Kept so every payload written as this type still deserializes.
/// </remarks>
[Obsolete("No longer raised: iteration state is worked out when read. Kept only to deserialize payloads already written as this type.")]
public sealed record IterationStateChangedEvent : DomainEvent<IterationStateChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    public IterationStateChangedEvent(Guid id, int key, IterationState fromState, IterationState toState, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromState = fromState;
        ToState = toState;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public IterationState FromState { get; }
    public IterationState ToState { get; }

    [JsonIgnore]
    public string AggregateType => "Iteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
