using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>
/// The iteration moved between Future, Active and Completed.
/// </summary>
public sealed record IterationStateChangedEvent : DomainEvent, IAggregateEvent
{
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
