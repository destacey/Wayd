using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>
/// The team started the sprint: it committed to its scope at <see cref="Started"/>.
/// </summary>
public sealed record SprintStartedEvent : DomainEvent<SprintStartedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    public SprintStartedEvent(Guid id, int key, Instant started, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Started = started;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Instant Started { get; }

    [JsonIgnore]
    public string AggregateType => "Iteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
