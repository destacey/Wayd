using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>
/// The team closed the sprint at <see cref="Completed"/>, either directly or by starting the next one.
/// </summary>
public sealed record SprintCompletedEvent : DomainEvent<SprintCompletedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    public SprintCompletedEvent(Guid id, int key, Instant completed, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Completed = completed;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Instant Completed { get; }

    [JsonIgnore]
    public string AggregateType => "Iteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
