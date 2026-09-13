using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;
using NodaTime;

namespace Wayd.Common.Domain.Events.WorkManagement.WorkIterations;

/// <summary>
/// The Work copy of an iteration moved between Future, Active and Completed.
/// </summary>
public sealed record WorkIterationStateChangedEvent : DomainEvent<WorkIterationStateChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    public WorkIterationStateChangedEvent(Guid id, int key, IterationState fromState, IterationState toState, EventActor actor, Instant timestamp)
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
    public string AggregateType => "WorkIteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
