using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.Planning.Iterations;
using NodaTime;

namespace Wayd.Common.Domain.Events.WorkManagement.WorkIterations;

[Obsolete("Superseded by WorkIterationDetailsUpdatedEvent, WorkIterationDateRangeChangedEvent, WorkIterationStateChangedEvent and WorkIterationTeamChangedEvent. Kept only to deserialize payloads already written as this type.")]
public sealed record WorkIterationUpdatedEvent : DomainEvent<WorkIterationUpdatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public WorkIterationUpdatedEvent(Guid id, string name, IterationType type, IterationState state, IterationDateRangeV1 dateRange, Guid? teamId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;
        Type = type;
        State = state;
        DateRange = dateRange;
        TeamId = teamId;
        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public string Name { get; }
    public IterationType Type { get; }
    public IterationState State { get; }
    public IterationDateRangeV1 DateRange { get; }
    public Guid? TeamId { get; }

    [JsonIgnore]
    public string AggregateType => "WorkIteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
