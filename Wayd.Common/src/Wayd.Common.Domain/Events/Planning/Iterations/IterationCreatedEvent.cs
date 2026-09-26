using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>
/// An iteration was created.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="IterationCreatedEventV2"/> replaced it. Kept so
/// every payload written as this type still deserializes into it — its name and members are the contract
/// those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by IterationCreatedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record IterationCreatedEvent : DomainEvent<IterationCreatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public IterationCreatedEvent(Guid id, int key, string name, IterationType type, IterationState state, IterationDateRangeV1 dateRange, Guid? teamId, EventActor actor, Instant timestamp)
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
