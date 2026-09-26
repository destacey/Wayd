using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Planning.Iterations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

[Obsolete("Superseded by IterationDetailsUpdatedEvent, IterationDateRangeChangedEvent, IterationStateChangedEvent and IterationTeamChangedEvent. Kept only to deserialize payloads already written as this type.")]
public sealed record IterationUpdatedEvent : DomainEvent<IterationUpdatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public IterationUpdatedEvent(Guid id, int key, string name, IterationType type, IterationState state, IterationDateRangeV1 dateRange, Guid? teamId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Type = type;
        State = state;
        DateRange = dateRange;
        TeamId = teamId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public IterationType Type { get; }
    public IterationState State { get; }
    public IterationDateRangeV1 DateRange { get; }
    public Guid? TeamId { get; }

    [JsonIgnore]
    public string AggregateType => "Iteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
